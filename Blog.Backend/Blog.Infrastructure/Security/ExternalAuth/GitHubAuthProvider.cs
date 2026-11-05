using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Blog.Application.Common.Exceptions;
using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.Security.ExternalAuth
{
    /// <summary>
    /// GitHub OAuth App：authorization code + PKCE。
    ///
    /// 本次只读公开 profile 的数字 id，不申请 scope，不请求邮箱；
    /// 拿到 id 后立即撤销 provider token，不落库。
    /// </summary>
    public sealed class GitHubAuthProvider : IExternalAuthProvider
    {
        private const string AuthorizeUrl = "https://github.com/login/oauth/authorize";
        private const string TokenUrl = "https://github.com/login/oauth/access_token";
        private const string UserApiUrl = "https://api.github.com/user";
        private const string RevokeApiUrl = "https://api.github.com/applications";
        private const string HttpClientName = "external-auth-github";
        private const string UserAgent = "BlogAdminLogin/1.0";

        private readonly ExternalAuthOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GitHubAuthProvider> _logger;

        public GitHubAuthProvider(
            ExternalAuthOptions options,
            IHttpClientFactory httpClientFactory,
            ILogger<GitHubAuthProvider> logger)
        {
            _options = options;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public ExternalAuthProvider Provider => ExternalAuthProvider.GitHub;
        public bool IsEnabled => _options.GitHub.Enabled;
        public bool SupportsQr => false;

        public string BuildAuthorizationUrl(ExternalAuthAuthorizationRequest request)
        {
            var options = _options.GitHub;
            var query = new List<string>
            {
                $"client_id={Uri.EscapeDataString(options.ClientId)}",
                $"redirect_uri={Uri.EscapeDataString(options.RedirectUri)}",
                "response_type=code",
                $"state={Uri.EscapeDataString(request.State)}",
            };

            if (!string.IsNullOrWhiteSpace(request.CodeChallenge))
            {
                query.Add($"code_challenge={Uri.EscapeDataString(request.CodeChallenge)}");
                query.Add("code_challenge_method=S256");
            }

            if (!string.IsNullOrWhiteSpace(options.Scopes))
                query.Add($"scope={Uri.EscapeDataString(options.Scopes)}");

            return $"{AuthorizeUrl}?{string.Join("&", query)}";
        }

        public async Task<ExternalIdentity> ExchangeCodeAsync(
            ExternalAuthCodeRequest request,
            CancellationToken cancellationToken = default)
        {
            var options = _options.GitHub;
            if (!options.Enabled)
                throw new ExternalAuthException("GitHub 未启用");

            using var client = CreateClient();
            var accessToken = await ExchangeTokenAsync(client, options, request, cancellationToken);

            try
            {
                var userId = await GetUserIdAsync(client, accessToken, cancellationToken);
                return new ExternalIdentity(ExternalAuthProvider.GitHub, userId);
            }
            finally
            {
                await RevokeTokenAsync(client, options, accessToken, cancellationToken);
            }
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        private static async Task<string> ExchangeTokenAsync(
            HttpClient client,
            GitHubExternalAuthOptions options,
            ExternalAuthCodeRequest request,
            CancellationToken cancellationToken)
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["code"] = request.Code,
                ["redirect_uri"] = options.RedirectUri,
            };

            if (!string.IsNullOrWhiteSpace(request.CodeVerifier))
                form["code_verifier"] = request.CodeVerifier;

            using var response = await client.PostAsync(
                TokenUrl, new FormUrlEncodedContent(form), cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ExternalAuthException($"GitHub token 交换失败（HTTP {(int)response.StatusCode}）");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                var description = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;
                throw new ExternalAuthException($"GitHub token 交换失败：{error.GetString()} {description}".Trim());
            }

            if (!root.TryGetProperty("access_token", out var token) || string.IsNullOrWhiteSpace(token.GetString()))
                throw new ExternalAuthException("GitHub token 响应缺少 access_token");

            return token.GetString()!;
        }

        private static async Task<string> GetUserIdAsync(
            HttpClient client,
            string accessToken,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ExternalAuthException($"GitHub 用户信息失败（HTTP {(int)response.StatusCode}）");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number)
                throw new ExternalAuthException("GitHub 用户信息缺少数字 id");

            return id.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 登录不需要 GitHub token；用完立即撤销，减少 token 泄露面。
        /// 撤销失败不影响登录，只记 Warning（不记录 token）。
        /// </summary>
        private async Task RevokeTokenAsync(
            HttpClient client,
            GitHubExternalAuthOptions options,
            string accessToken,
            CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"{RevokeApiUrl}/{Uri.EscapeDataString(options.ClientId)}/token");

                var basic = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
                request.Content = JsonContent.Create(new { access_token = accessToken });

                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("GitHub token 撤销返回 HTTP {Status}", (int)response.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning("GitHub token 撤销失败：{Message}", ex.Message);
            }
        }
    }
}
