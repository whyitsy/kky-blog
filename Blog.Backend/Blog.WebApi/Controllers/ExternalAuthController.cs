using Blog.Application.Common;
using Blog.Application.Common.Exceptions;
using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Blog.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blog.WebApi.Controllers
{
    /// <summary>
    /// 第三方登录 / 绑定。
    ///
    /// 浏览器会经历两次跳转：
    ///   登录：本站 → provider → /api/auth/external/{provider}/callback → /auth/callback → 前端换 JWT
    ///   绑定：已登录管理员 → provider → callback → /admin/security
    ///
    /// 第三方 code / token 不出现在前端 URL；callback 只设置 HttpOnly 的一次性票据 cookie。
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class ExternalAuthController : ControllerBase
    {
        private readonly IExternalAuthService _externalAuth;
        private readonly ExternalAuthCookieStore _cookies;
        private readonly ExternalAuthOptions _options;
        private readonly ICurrentUser _currentUser;
        private readonly ILogger<ExternalAuthController> _logger;

        public ExternalAuthController(
            IExternalAuthService externalAuth,
            ExternalAuthCookieStore cookies,
            ExternalAuthOptions options,
            ICurrentUser currentUser,
            ILogger<ExternalAuthController> logger)
        {
            _externalAuth = externalAuth;
            _cookies = cookies;
            _options = options;
            _currentUser = currentUser;
            _logger = logger;
        }

        /// <summary>登录页从配置读取启用的 provider；不含任何密钥。</summary>
        [HttpGet("providers")]
        [AllowAnonymous]
        public async Task<ApiResponse<List<ExternalAuthProviderDto>>> Providers(CancellationToken cancellationToken)
        {
            var providers = await _externalAuth.GetProvidersAsync(cancellationToken);
            return ApiResponse<List<ExternalAuthProviderDto>>.Ok(providers);
        }

        /// <summary>开始网页跳转登录。必须是顶层导航，所以直接 302。</summary>
        [HttpGet("external/{provider}/start")]
        [AllowAnonymous]
        public async Task<IActionResult> Start(
            string provider,
            [FromQuery] string? returnUrl,
            CancellationToken cancellationToken)
        {
            // 页面可能从 127.0.0.1 打开，而 provider 回调配置的是 localhost。
            // Cookie 按 host 隔离，必须先把用户带到回调配置的 host，否则 callback 收不到 state。
            var canonicalStart = BuildCanonicalStartRedirect(provider, returnUrl);
            if (canonicalStart is not null)
                return Redirect(canonicalStart);

            try
            {
                var result = await _externalAuth.StartAsync(
                    provider,
                    ExternalAuthIntent.Login,
                    ExternalAuthFlow.Redirect,
                    returnUrl,
                    bindUserId: null,
                    cancellationToken);

                _cookies.SetState(Response, result.State);
                return Redirect(result.AuthorizationUrl);
            }
            catch (BusinessException ex)
            {
                _logger.LogWarning("第三方登录启动失败 {Provider}: {Message}", provider, ex.Message);
                return RedirectToFrontend("/login", ("error", "external_unavailable"), ("provider", provider));
            }
        }

        /// <summary>管理员绑定：fetch 带 Bearer 拿授权 URL，再整页跳转。</summary>
        [HttpPost("external/{provider}/bind/start")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<ExternalAuthUrlDto>> BindStart(
            string provider,
            [FromBody] ExternalAuthStartRequest? request,
            CancellationToken cancellationToken)
        {
            EnsureCanonicalHost(provider);

            var userId = _currentUser.UserId
                         ?? throw new BusinessException("未登录", ErrorCodes.Unauthorized);

            var result = await _externalAuth.StartAsync(
                provider,
                ExternalAuthIntent.Bind,
                ExternalAuthFlow.Redirect,
                request?.ReturnUrl,
                userId,
                cancellationToken);

            _cookies.SetState(Response, result.State);
            return ApiResponse<ExternalAuthUrlDto>.Ok(new ExternalAuthUrlDto(result.AuthorizationUrl));
        }

        /// <summary>飞书扫码：返回旧版 passport 授权 URL，前端用官方 QR SDK 渲染二维码。</summary>
        [HttpPost("external/feishu/qr/start")]
        [AllowAnonymous]
        public async Task<ApiResponse<ExternalAuthUrlDto>> FeishuQrStart(
            [FromBody] ExternalAuthStartRequest? request,
            CancellationToken cancellationToken)
        {
            EnsureCanonicalHost("feishu");

            var result = await _externalAuth.StartAsync(
                "feishu",
                ExternalAuthIntent.Login,
                ExternalAuthFlow.Qr,
                request?.ReturnUrl,
                bindUserId: null,
                cancellationToken);

            _cookies.SetState(Response, result.State);
            return ApiResponse<ExternalAuthUrlDto>.Ok(new ExternalAuthUrlDto(result.AuthorizationUrl));
        }

        /// <summary>
        /// provider 回调。无论成功失败都 302 回前端，不能把第三方 JSON/错误页暴露给用户。
        /// </summary>
        [HttpGet("external/{provider}/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(
            string provider,
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
        {
            _cookies.TryGetState(Request, out var cookieState);

            var result = await _externalAuth.HandleCallbackAsync(
                provider, code, state, error, cookieState, cancellationToken);

            _cookies.ClearState(Response);

            if (result.Outcome == ExternalAuthCallbackOutcome.LoginSucceeded && result.UserId is not null)
            {
                _cookies.SetLoginTicket(Response, result.UserId.Value);
                return RedirectToFrontend(
                    "/auth/callback",
                    ("returnUrl", result.ReturnUrl));
            }

            if (result.Outcome == ExternalAuthCallbackOutcome.BindSucceeded)
                return RedirectToFrontend("/admin/security", ("binding", "success"));

            if (result.Intent == ExternalAuthIntent.Bind)
                return RedirectToFrontend(
                    "/admin/security",
                    ("binding", "error"),
                    ("reason", result.Error ?? "bind_failed"));

            return RedirectToFrontend(
                "/login",
                ("error", result.Error ?? "external_failed"),
                ("provider", provider));
        }

        /// <summary>用一次性登录票据换本站 JWT。</summary>
        [HttpPost("external/complete")]
        [AllowAnonymous]
        public async Task<ApiResponse<LoginResponse>> Complete(CancellationToken cancellationToken)
        {
            if (!_cookies.TryGetLoginTicket(Request, out var userId))
                throw new BusinessException("登录票据无效或已过期，请重新登录", ErrorCodes.Unauthorized);

            try
            {
                var result = await _externalAuth.CompleteLoginAsync(userId, cancellationToken);
                return ApiResponse<LoginResponse>.Ok(result);
            }
            finally
            {
                _cookies.ClearLoginTicket(Response);
            }
        }

        /// <summary>当前管理员的第三方绑定状态（只返回 provider + 时间，不含第三方资料）。</summary>
        [HttpGet("external/bindings")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<List<ExternalAuthBindingDto>>> Bindings(CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                         ?? throw new BusinessException("未登录", ErrorCodes.Unauthorized);

            var bindings = await _externalAuth.GetBindingsAsync(userId, cancellationToken);
            return ApiResponse<List<ExternalAuthBindingDto>>.Ok(bindings);
        }

        /// <summary>解绑当前账号的某个平台。</summary>
        [HttpDelete("external/bindings/{provider}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<object?>> Unbind(string provider, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                         ?? throw new BusinessException("未登录", ErrorCodes.Unauthorized);

            if (!Enum.TryParse<ExternalAuthProvider>(provider, ignoreCase: true, out var parsed))
                throw new BusinessException("不支持的第三方登录平台", ErrorCodes.InvalidArgument);

            await _externalAuth.UnbindAsync(userId, parsed, cancellationToken);
            return ApiResponse.Ok();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// 如果当前页面 host 与 provider 回调 host 不一致，返回回调 host 上的 start 地址。
        /// 不做这件事时，从 127.0.0.1 发起登录会写 127 的 state cookie，而 GitHub 回调到
        /// localhost，callback 读不到 cookie，最终表现为 error=invalid_state。
        /// </summary>
        private string? BuildCanonicalStartRedirect(string provider, string? returnUrl)
        {
            if (!TryGetProviderRedirectUri(provider, out var redirectUri)) return null;

            var requestHost = Request.Host.Host;
            if (string.IsNullOrWhiteSpace(requestHost)
                || string.Equals(requestHost, redirectUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var query = string.IsNullOrWhiteSpace(returnUrl)
                ? string.Empty
                : $"?returnUrl={Uri.EscapeDataString(returnUrl)}";

            return $"{redirectUri.Scheme}://{redirectUri.Authority}/api/auth/external/{provider}/start{query}";
        }

        /// <summary>绑定/扫码是 fetch，无法用 302 自动换 host；host 不一致时给明确错误。</summary>
        private void EnsureCanonicalHost(string provider)
        {
            if (!TryGetProviderRedirectUri(provider, out var redirectUri)) return;

            var requestHost = Request.Host.Host;
            if (string.IsNullOrWhiteSpace(requestHost)
                || string.Equals(requestHost, redirectUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new BusinessException(
                $"当前访问地址（{requestHost}）与第三方回调地址（{redirectUri.Host}）不一致，请改用 {redirectUri.Scheme}://{redirectUri.Authority} 访问后再操作",
                ErrorCodes.BusinessRule);
        }

        private bool TryGetProviderRedirectUri(string provider, out Uri redirectUri)
        {
            redirectUri = null!;

            var raw = provider.Equals("feishu", StringComparison.OrdinalIgnoreCase)
                ? _options.Feishu.RedirectUri
                : provider.Equals("github", StringComparison.OrdinalIgnoreCase)
                    ? _options.GitHub.RedirectUri
                    : string.Empty;

            return Uri.TryCreate(raw, UriKind.Absolute, out redirectUri!);
        }

        private string FrontendUrl(string path, params (string Key, string? Value)[] query)
        {
            var queryString = string.Join("&", query
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));

            var relative = queryString.Length == 0 ? path : $"{path}?{queryString}";

            return string.IsNullOrWhiteSpace(_options.FrontendBaseUrl)
                ? relative
                : $"{_options.FrontendBaseUrl.TrimEnd('/')}{relative}";
        }

        private RedirectResult RedirectToFrontend(string path, params (string Key, string? Value)[] query) =>
            Redirect(FrontendUrl(path, query));
    }
}
