namespace Blog.Application.Interfaces
{
    /// <summary>第三方登录配置（对应 appsettings 的 ExternalAuth 节）。</summary>
    public sealed class ExternalAuthOptions
    {
        public const string SectionName = "ExternalAuth";

        /// <summary>
        /// 回调成功后跳回前端的基地址。前后端同源时可留空（使用相对路径跳转）。
        /// 例：https://blog.example.com
        /// </summary>
        public string FrontendBaseUrl { get; set; } = string.Empty;

        /// <summary>OAuth state cookie 的有效期（分钟）</summary>
        public int StateMinutes { get; set; } = 10;

        /// <summary>登录成功后一次性票据 cookie 的有效期（分钟）</summary>
        public int LoginTicketMinutes { get; set; } = 2;

        public GitHubExternalAuthOptions GitHub { get; set; } = new();

        public FeishuExternalAuthOptions Feishu { get; set; } = new();

        /// <summary>
        /// 启动期校验：启用的 provider 必须有凭据与回调地址；否则拒绝启动。
        /// 与 Jwt:SigningKey 的“失败前置”保持一致，避免运行时才发现登录不可用。
        /// </summary>
        public void Validate()
        {
            ValidateFrontendBaseUrl();
            ValidateCommon(GitHub.Enabled, GitHub.ClientId, GitHub.ClientSecret, GitHub.RedirectUri, "GitHub");
            ValidateCommon(Feishu.Enabled, Feishu.AppId, Feishu.AppSecret, Feishu.RedirectUri, "Feishu");
        }

        private void ValidateFrontendBaseUrl()
        {
            if (string.IsNullOrWhiteSpace(FrontendBaseUrl)) return;
            if (!Uri.TryCreate(FrontendBaseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "ExternalAuth:FrontendBaseUrl 必须是 http/https 绝对地址，或留空表示同源。");
            }
        }

        private static void ValidateCommon(bool enabled, string id, string secret, string redirectUri, string provider)
        {
            if (!enabled) return;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException(
                    $"ExternalAuth:{provider} 已启用，但 AppId/ClientId 或 Secret 未配置。请通过环境变量注入，不要写进 appsettings。");

            if (string.IsNullOrWhiteSpace(redirectUri)
                || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"ExternalAuth:{provider}:RedirectUri 必须是 http/https 绝对地址。");
            }
        }
    }

    public sealed class GitHubExternalAuthOptions
    {
        public bool Enabled { get; set; }

        public string ClientId { get; set; } = string.Empty;

        /// <summary>敏感值，必须来自环境变量 / user-secrets，禁止入库。</summary>
        public string ClientSecret { get; set; } = string.Empty;

        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>留空表示不申请 scope（只读公开 profile 的 id）。</summary>
        public string Scopes { get; set; } = string.Empty;
    }

    public sealed class FeishuExternalAuthOptions
    {
        public bool Enabled { get; set; }

        public string AppId { get; set; } = string.Empty;

        /// <summary>敏感值，必须来自环境变量 / user-secrets，禁止入库。</summary>
        public string AppSecret { get; set; } = string.Empty;

        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>可留空；填写后只允许该租户的用户登录。</summary>
        public string TenantKey { get; set; } = string.Empty;

        /// <summary>基础 user_info 字段不需要 scope，默认留空。</summary>
        public string Scopes { get; set; } = string.Empty;

        /// <summary>是否在登录页显示飞书扫码入口。</summary>
        public bool EnableQr { get; set; } = true;
    }
}
