using System.Security.Cryptography;
using System.Text;
using Blog.Application.Common;
using Blog.Application.Common.Exceptions;
using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Blog.Domain.IRepository;

namespace Blog.Application.Services.Auth
{
    /// <summary>
    /// 第三方登录/绑定的统一编排。
    ///
    /// provider 只负责「拼授权 URL / 用 code 换身份」，这里负责：
    ///   - state / 过期 / provider 一致性校验；
    ///   - 飞书租户校验；
    ///   - 只绑定已有账号（绝不自动注册）；
    ///   - 唯一绑定约束与登录审计；
    ///   - 用现有 JWT 签发本站会话。
    /// </summary>
    public sealed class ExternalAuthService : IExternalAuthService
    {
        private readonly Dictionary<ExternalAuthProvider, IExternalAuthProvider> _providers;
        private readonly IExternalLoginRepository _externalLogins;
        private readonly IUserRepository _users;
        private readonly IAuthorRepository _authors;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _uow;
        private readonly ExternalAuthOptions _options;

        public ExternalAuthService(
            IEnumerable<IExternalAuthProvider> providers,
            IExternalLoginRepository externalLogins,
            IUserRepository users,
            IAuthorRepository authors,
            ITokenService tokenService,
            IUnitOfWork uow,
            ExternalAuthOptions options)
        {
            _providers = providers.ToDictionary(p => p.Provider);
            _externalLogins = externalLogins;
            _users = users;
            _authors = authors;
            _tokenService = tokenService;
            _uow = uow;
            _options = options;
        }

        public Task<List<ExternalAuthProviderDto>> GetProvidersAsync(CancellationToken cancellationToken = default)
        {
            var result = _providers.Values
                .Where(p => p.IsEnabled)
                .OrderBy(p => p.Provider)
                .Select(p => new ExternalAuthProviderDto(
                    p.Provider.ToString(),
                    ProviderDisplayName(p.Provider),
                    p.SupportsQr))
                .ToList();

            return Task.FromResult(result);
        }

        public Task<ExternalAuthStartResult> StartAsync(
            string provider,
            ExternalAuthIntent intent,
            ExternalAuthFlow flow,
            string? returnUrl,
            Guid? bindUserId,
            CancellationToken cancellationToken = default)
        {
            var authProvider = GetProvider(provider);

            if (intent == ExternalAuthIntent.Bind && (bindUserId is null || bindUserId == Guid.Empty))
                throw new BusinessException("绑定操作缺少当前账号", ErrorCodes.Unauthorized);

            if (flow == ExternalAuthFlow.Qr && !authProvider.SupportsQr)
                throw new BusinessException("该登录方式不支持扫码", ErrorCodes.InvalidArgument);

            var normalizedReturnUrl = NormalizeReturnUrl(returnUrl);
            var stateValue = RandomToken(32);
            var verifier = flow == ExternalAuthFlow.Redirect ? RandomToken(32) : null;
            var challenge = verifier is null ? null : Sha256Base64Url(verifier);

            var authorizationUrl = authProvider.BuildAuthorizationUrl(
                new ExternalAuthAuthorizationRequest(stateValue, challenge, flow));

            var state = new ExternalAuthState(
                authProvider.Provider,
                intent,
                flow,
                stateValue,
                verifier,
                normalizedReturnUrl,
                bindUserId,
                DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.StateMinutes, 1, 30)));

            return Task.FromResult(new ExternalAuthStartResult(authorizationUrl, state));
        }

        public async Task<ExternalAuthCallbackResult> HandleCallbackAsync(
            string provider,
            string? code,
            string? state,
            string? error,
            ExternalAuthState? cookieState,
            CancellationToken cancellationToken = default)
        {
            var intent = cookieState?.Intent ?? ExternalAuthIntent.Login;

            if (cookieState is null)
                return Failed(intent, "invalid_state");

            if (cookieState.ExpiresAt <= DateTimeOffset.UtcNow)
                return Failed(intent, "state_expired");

            if (!string.Equals(cookieState.State, state, StringComparison.Ordinal))
                return Failed(intent, "invalid_state");

            if (!string.IsNullOrWhiteSpace(error))
                return Failed(intent, error == "access_denied" ? "external_denied" : "external_failed");

            if (string.IsNullOrWhiteSpace(code))
                return Failed(intent, "invalid_code");

            try
            {
                var authProvider = GetProvider(provider);
                if (authProvider.Provider != cookieState.Provider)
                    return Failed(intent, "provider_mismatch");

                var identity = await authProvider.ExchangeCodeAsync(
                    new ExternalAuthCodeRequest(code, cookieState.CodeVerifier, cookieState.Flow),
                    cancellationToken);

                if (identity.Provider != cookieState.Provider || string.IsNullOrWhiteSpace(identity.ProviderUserId))
                    return Failed(intent, "identity_invalid");

                // 飞书自建应用天然只服务本租户；配置了 TenantKey 时再做一道显式校验。
                if (cookieState.Provider == ExternalAuthProvider.Feishu
                    && !string.IsNullOrWhiteSpace(_options.Feishu.TenantKey)
                    && !string.Equals(identity.TenantKey, _options.Feishu.TenantKey, StringComparison.Ordinal))
                {
                    return Failed(intent, "tenant_mismatch");
                }

                if (cookieState.Intent == ExternalAuthIntent.Bind)
                {
                    if (cookieState.BindUserId is null || cookieState.BindUserId == Guid.Empty)
                        return Failed(intent, "bind_failed");

                    try
                    {
                        await BindAsync(cookieState.BindUserId.Value, identity, cancellationToken);
                        return new ExternalAuthCallbackResult(
                            ExternalAuthCallbackOutcome.BindSucceeded,
                            ExternalAuthIntent.Bind,
                            cookieState.BindUserId,
                            cookieState.ReturnUrl,
                            null);
                    }
                    catch (BusinessException ex)
                    {
                        return Failed(intent, ex.Code == ErrorCodes.DuplicateResource ? "bind_conflict" : "bind_failed");
                    }
                }

                var binding = await _externalLogins.GetByProviderUserAsync(
                    identity.Provider, identity.ProviderUserId, cancellationToken);

                if (binding is null)
                    return new ExternalAuthCallbackResult(
                        ExternalAuthCallbackOutcome.Unbound,
                        ExternalAuthIntent.Login,
                        null,
                        cookieState.ReturnUrl,
                        "external_unbound");

                var user = await _users.GetByIdAsync(binding.UserId, cancellationToken);
                if (user is null || !user.IsActive)
                    return Failed(intent, "account_unavailable");

                binding.MarkLoggedIn();
                await _uow.SaveChangesAsync(cancellationToken);

                return new ExternalAuthCallbackResult(
                    ExternalAuthCallbackOutcome.LoginSucceeded,
                    ExternalAuthIntent.Login,
                    user.Id,
                    cookieState.ReturnUrl,
                    null);
            }
            catch (BusinessException ex)
            {
                return Failed(intent, ex.Code == ErrorCodes.NotFound ? "provider_not_found" : "external_failed");
            }
            catch (ExternalAuthException)
            {
                return Failed(intent, "external_failed");
            }
            catch (HttpRequestException)
            {
                return Failed(intent, "external_unavailable");
            }
            catch (TaskCanceledException)
            {
                return Failed(intent, "external_unavailable");
            }
        }

        public async Task<LoginResponse> CompleteLoginAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user is null || !user.IsActive)
                throw new BusinessException("账号不可用，请重新登录", ErrorCodes.Unauthorized);

            user.MarkLoggedIn();
            await _uow.SaveChangesAsync(cancellationToken);

            var (token, expiresAt) = _tokenService.Issue(user);
            var authorName = await GetAuthorNameAsync(user, cancellationToken);

            return new LoginResponse(
                token,
                expiresAt,
                user.Role.ToString(),
                new CurrentUserDto(
                    user.Id,
                    user.Email,
                    user.Role.ToString(),
                    user.IsActive,
                    user.AuthorId,
                    authorName,
                    user.LastLoginAt));
        }

        public async Task<List<ExternalAuthBindingDto>> GetBindingsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var bindings = await _externalLogins.GetByUserAsync(userId, cancellationToken);

            return bindings
                .Select(b => new ExternalAuthBindingDto(
                    b.Provider.ToString(),
                    ProviderDisplayName(b.Provider),
                    b.CreatedAt,
                    b.LastLoginAt))
                .ToList();
        }

        public async Task BindAsync(
            Guid userId,
            ExternalIdentity identity,
            CancellationToken cancellationToken = default)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user is null || !user.IsActive)
                throw new BusinessException("账号不可用", ErrorCodes.Unauthorized);

            if (string.IsNullOrWhiteSpace(identity.ProviderUserId))
                throw new BusinessException("第三方身份无效", ErrorCodes.InvalidArgument);

            var sameIdentity = await _externalLogins.GetByProviderUserAsync(
                identity.Provider, identity.ProviderUserId, cancellationToken);

            if (sameIdentity is not null)
            {
                if (sameIdentity.UserId == userId) return;
                throw new BusinessException("该第三方账号已绑定其他本站账号", ErrorCodes.DuplicateResource);
            }

            var sameProvider = await _externalLogins.GetByUserAndProviderAsync(
                userId, identity.Provider, cancellationToken);

            if (sameProvider is not null)
            {
                if (sameProvider.ProviderUserId == identity.ProviderUserId) return;
                throw new BusinessException(
                    $"当前账号已绑定另一个{ProviderDisplayName(identity.Provider)}账号，请先解绑",
                    ErrorCodes.DuplicateResource);
            }

            await _externalLogins.AddAsync(
                new ExternalLogin(userId, identity.Provider, identity.ProviderUserId),
                cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        public async Task UnbindAsync(
            Guid userId,
            ExternalAuthProvider provider,
            CancellationToken cancellationToken = default)
        {
            var binding = await _externalLogins.GetByUserAndProviderAsync(userId, provider, cancellationToken)
                          ?? throw new BusinessException("尚未绑定该第三方账号", ErrorCodes.NotFound);

            _externalLogins.Remove(binding);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        // ---------------------------------------------------------------- helpers

        private IExternalAuthProvider GetProvider(string provider)
        {
            if (!Enum.TryParse<ExternalAuthProvider>(provider, ignoreCase: true, out var parsed))
                throw new BusinessException("不支持的第三方登录平台", ErrorCodes.InvalidArgument);

            if (!_providers.TryGetValue(parsed, out var authProvider) || !authProvider.IsEnabled)
                throw new BusinessException("该登录方式未启用", ErrorCodes.NotFound);

            return authProvider;
        }

        private async Task<string?> GetAuthorNameAsync(User user, CancellationToken cancellationToken)
        {
            if (!user.AuthorId.HasValue) return null;
            var author = await _authors.GetByIdAsync(user.AuthorId.Value, cancellationToken);
            return author?.Name;
        }

        private static ExternalAuthCallbackResult Failed(ExternalAuthIntent intent, string error) =>
            new(ExternalAuthCallbackOutcome.Failed, intent, null, null, error);

        private static string? NormalizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl)) return null;

            var value = returnUrl.Trim();
            if (value.Length > 500
                || !value.StartsWith('/')
                || value.StartsWith("//")
                || value.Contains('\\'))
            {
                throw new BusinessException("返回地址不合法", ErrorCodes.InvalidArgument);
            }

            return value;
        }

        private static string RandomToken(int byteCount)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteCount);
            return Base64Url(bytes);
        }

        private static string Sha256Base64Url(string value)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(value));
            return Base64Url(hash);
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string ProviderDisplayName(ExternalAuthProvider provider) => provider switch
        {
            ExternalAuthProvider.GitHub => "GitHub",
            ExternalAuthProvider.Feishu => "飞书",
            _ => provider.ToString(),
        };
    }
}
