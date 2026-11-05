using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blog.Application.Common.Exceptions;
using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.Security.ExternalAuth
{
    /// <summary>
    /// 飞书企业自建应用。
    ///
    /// 两条链路都只取 open_id（以及临时用于校验的 tenant_key）：
    ///   - 网页跳转：新版 v3 authorize + v3 token；
    ///   - 扫码：官方二维码 SDK 使用的旧 passport 授权 + app_access_token + 旧 OIDC token。
    ///
    /// 基础 user_info 字段不需要任何通讯录 scope；不读取/不存储姓名、头像、邮箱、手机号。
    /// </summary>
    public sealed class FeishuAuthProvider : IExternalAuthProvider
    {
        private const string V3AuthorizeUrl = "https://accounts.feishu.cn/open-apis/authen/v1/authorize";
        private const string LegacyAuthorizeUrl = "https://passport.feishu.cn/suite/passport/oauth/authorize";
        private const string AppAccessTokenUrl = "https://open.feishu.cn/open-apis/auth/v3/app_access_token/internal";
        private const string OldOidcTokenUrl = "https://open.feishu.cn/open-apis/authen/v1/oidc/access_token";
        private const string V3TokenUrl = "https://accounts.feishu.cn/oauth/v3/token";
        private const string UserInfoUrl = "https://open.feishu.cn/open-apis/authen/v1/user_info";
        private const string HttpClientName = "external-auth-feishu";
        private const string UserAgent = "BlogAdminLogin/1.0";

        private readonly ExternalAuthOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FeishuAuthProvider> _logger;

        public FeishuAuthProvider(
            ExternalAuthOptions options,
            IHttpClientFactory httpClientFactory,
            ILogger<FeishuAuthProvider> logger)
        {
            _options = options;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public ExternalAuthProvider Provider => ExternalAuthProvider.Feishu;
        public bool IsEnabled => _options.Feishu.Enabled;
        public bool SupportsQr => _options.Feishu.Enabled && _options.Feishu.EnableQr;

        public string BuildAuthorizationUrl(ExternalAuthAuthorizationRequest request)
        {
            var options = _options.Feishu;
            var authorizeUrl = request.Flow == ExternalAuthFlow.Qr ? LegacyAuthorizeUrl : V3AuthorizeUrl;

            var query = new List<string>
            {
                $"client_id={Uri.EscapeDataString(options.AppId)}",
                $"redirect_uri={Uri.EscapeDataString(options.RedirectUri)}",
                "response_type=code",
                $"state={Uri.EscapeDataString(request.State)}",
            };

            // 扫码旧链路不支持 scope；网页跳转按需追加已开通的 scope。
            if (request.Flow != ExternalAuthFlow.Qr && !string.IsNullOrWhiteSpace(options.Scopes))
                query.Add($"scope={Uri.EscapeDataString(options.Scopes)}");

            return $"{authorizeUrl}?{string.Join("&", query)}";
        }

        public async Task<ExternalIdentity> ExchangeCodeAsync(
            ExternalAuthCodeRequest request,
            CancellationToken cancellationToken = default)
        {
            var options = _options.Feishu;
            if (!options.Enabled)
                throw new ExternalAuthException("飞书未启用");

            using var client = CreateClient();

            var userAccessToken = request.Flow == ExternalAuthFlow.Qr
                ? await ExchangeLegacyQrTokenAsync(client, options, request.Code, cancellationToken)
                : await ExchangeV3TokenAsync(client, options, request.Code, cancellationToken);

            return await GetUserIdentityAsync(client, userAccessToken, cancellationToken);
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        /// <summary>
        /// 官方二维码教程链路：
        /// app_access_token/internal → authen/v1/oidc/access_token。
        /// </summary>
        private static async Task<string> ExchangeLegacyQrTokenAsync(
            HttpClient client,
            FeishuExternalAuthOptions options,
            string code,
            CancellationToken cancellationToken)
        {
            using var appResponse = await client.PostAsJsonAsync(
                AppAccessTokenUrl,
                new { app_id = options.AppId, app_secret = options.AppSecret },
                cancellationToken);
            using var appDoc = await ReadFeishuJsonAsync(appResponse, "飞书 app_access_token", cancellationToken);
            var appRoot = appDoc.RootElement;

            var appToken = ReadString(appRoot, "app_access_token")
                           ?? ReadString(appRoot, "tenant_access_token")
                           ?? throw new ExternalAuthException("飞书 app_access_token 响应缺少应用凭证");

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, OldOidcTokenUrl);
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
            tokenRequest.Content = JsonContent.Create(new
            {
                grant_type = "authorization_code",
                code,
            });

            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            using var tokenDoc = await ReadFeishuJsonAsync(tokenResponse, "飞书 user_access_token", cancellationToken);
            var tokenRoot = tokenDoc.RootElement;

            if (tokenRoot.TryGetProperty("data", out var data))
                return ReadString(data, "access_token")
                       ?? throw new ExternalAuthException("飞书 user_access_token 响应缺少 access_token");

            return ReadString(tokenRoot, "access_token")
                   ?? throw new ExternalAuthException("飞书 user_access_token 响应缺少 access_token");
        }

        /// <summary>新版网页跳转链路：accounts.feishu.cn/oauth/v3/token。</summary>
        private static async Task<string> ExchangeV3TokenAsync(
            HttpClient client,
            FeishuExternalAuthOptions options,
            string code,
            CancellationToken cancellationToken)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = options.AppId,
                ["client_secret"] = options.AppSecret,
                ["code"] = code,
                ["redirect_uri"] = options.RedirectUri,
            };

            using var response = await client.PostAsync(
                V3TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
            using var doc = await ReadFeishuJsonAsync(response, "飞书 v3 token", cancellationToken);
            var root = doc.RootElement;

            return ReadString(root, "access_token")
                   ?? throw new ExternalAuthException("飞书 v3 token 响应缺少 access_token");
        }

        private static async Task<ExternalIdentity> GetUserIdentityAsync(
            HttpClient client,
            string userAccessToken,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            using var doc = await ReadFeishuJsonAsync(response, "飞书 user_info", cancellationToken);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data))
                throw new ExternalAuthException("飞书 user_info 响应缺少 data");

            var openId = ReadString(data, "open_id")
                         ?? throw new ExternalAuthException("飞书 user_info 响应缺少 open_id");
            var tenantKey = ReadString(data, "tenant_key");

            return new ExternalIdentity(ExternalAuthProvider.Feishu, openId, tenantKey);
        }

        private static async Task<JsonDocument> ReadFeishuJsonAsync(
            HttpResponseMessage response,
            string operation,
            CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ExternalAuthException($"{operation} 失败（HTTP {(int)response.StatusCode}）");

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                throw new ExternalAuthException($"{operation} 返回了非 JSON 响应", ex);
            }

            if (doc.RootElement.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.Number
                && code.GetInt32() != 0)
            {
                var error = ReadString(doc.RootElement, "error");
                var description = ReadString(doc.RootElement, "error_description")
                                  ?? ReadString(doc.RootElement, "msg");
                doc.Dispose();
                throw new ExternalAuthException($"{operation} 返回错误：{code.GetInt32()} {error} {description}".Trim());
            }

            return doc;
        }

        private static string? ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
