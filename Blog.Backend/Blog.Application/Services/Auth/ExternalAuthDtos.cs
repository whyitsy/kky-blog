using Blog.Domain.Entities;

namespace Blog.Application.Services.Auth
{
    /// <summary>本次外部认证是登录还是给当前账号绑定。</summary>
    public enum ExternalAuthIntent
    {
        Login = 0,
        Bind = 1,
    }

    /// <summary>
    /// 飞书的两种授权入口：
    /// Redirect = 新版 v3 网页跳转；Qr = 官方二维码 SDK 使用的旧 passport 链路。
    /// GitHub 始终使用 Redirect。
    /// </summary>
    public enum ExternalAuthFlow
    {
        Redirect = 0,
        Qr = 1,
    }

    public enum ExternalAuthCallbackOutcome
    {
        /// <summary>已找到绑定，可以签发本站会话</summary>
        LoginSucceeded,

        /// <summary>已绑定到当前管理员账号</summary>
        BindSucceeded,

        /// <summary>第三方身份尚未绑定任何本站账号（不自动注册）</summary>
        Unbound,

        /// <summary>state 无效、provider 拒绝、租户不符等</summary>
        Failed,
    }

    /// <summary>登录页需要的 provider 能力（不含任何密钥）。</summary>
    public sealed record ExternalAuthProviderDto(
        string Provider,
        string DisplayName,
        bool SupportsQr);

    /// <summary>管理员绑定页需要的状态（不含第三方资料）。</summary>
    public sealed record ExternalAuthBindingDto(
        string Provider,
        string DisplayName,
        DateTimeOffset BoundAt,
        DateTimeOffset? LastLoginAt);

    /// <summary>
    /// 存在加密 State Cookie 里的 OAuth 上下文。
    /// 不包含任何第三方 token。
    /// </summary>
    public sealed record ExternalAuthState(
        ExternalAuthProvider Provider,
        ExternalAuthIntent Intent,
        ExternalAuthFlow Flow,
        string State,
        string? CodeVerifier,
        string? ReturnUrl,
        Guid? BindUserId,
        DateTimeOffset ExpiresAt);

    public sealed record ExternalAuthStartResult(
        string AuthorizationUrl,
        ExternalAuthState State);

    public sealed record ExternalAuthCallbackResult(
        ExternalAuthCallbackOutcome Outcome,
        ExternalAuthIntent Intent,
        Guid? UserId,
        string? ReturnUrl,
        string? Error);

    /// <summary>provider 归一化后的身份：只有平台 + 平台用户 Id（+ 飞书租户用于校验）。</summary>
    public sealed record ExternalIdentity(
        ExternalAuthProvider Provider,
        string ProviderUserId,
        string? TenantKey = null);

    /// <summary>provider 构造授权 URL 所需的信息。</summary>
    public sealed record ExternalAuthAuthorizationRequest(
        string State,
        string? CodeChallenge,
        ExternalAuthFlow Flow);

    /// <summary>provider 用 code 换取身份所需的信息。</summary>
    public sealed record ExternalAuthCodeRequest(
        string Code,
        string? CodeVerifier,
        ExternalAuthFlow Flow);

    /// <summary>绑定页 POST 的请求体。</summary>
    public sealed record ExternalAuthStartRequest(string? ReturnUrl);

    /// <summary>返回给前端的授权 URL（绑定/扫码入口用 fetch 获取）。</summary>
    public sealed record ExternalAuthUrlDto(string Url);
}
