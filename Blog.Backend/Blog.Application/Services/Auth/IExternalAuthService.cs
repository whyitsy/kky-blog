using Blog.Domain.Entities;

namespace Blog.Application.Services.Auth
{
    /// <summary>
    /// 第三方登录/绑定的应用服务。
    ///
    /// 关键约束：
    ///   - 只允许绑定已有账号，不自动注册；
    ///   - 只存登录证明（Provider + ProviderUserId + UserId）；
    ///   - 不保存第三方昵称/头像/邮箱/access_token。
    /// </summary>
    public interface IExternalAuthService
    {
        Task<List<ExternalAuthProviderDto>> GetProvidersAsync(CancellationToken cancellationToken = default);

        /// <summary>创建授权 URL 与 state（state 由 Web 层写入加密 cookie）。</summary>
        Task<ExternalAuthStartResult> StartAsync(
            string provider,
            ExternalAuthIntent intent,
            ExternalAuthFlow flow,
            string? returnUrl,
            Guid? bindUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 处理 provider 回调。cookieState 由 Web 层从加密 cookie 解出后传入。
        /// 任何 provider 异常都应转成 Failed，不能把第三方错误页/JSON 暴露给浏览器。
        /// </summary>
        Task<ExternalAuthCallbackResult> HandleCallbackAsync(
            string provider,
            string? code,
            string? state,
            string? error,
            ExternalAuthState? cookieState,
            CancellationToken cancellationToken = default);

        /// <summary>用一次性登录票据里的 UserId 签发本站会话。</summary>
        Task<LoginResponse> CompleteLoginAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<List<ExternalAuthBindingDto>> GetBindingsAsync(Guid userId, CancellationToken cancellationToken = default);

        Task BindAsync(Guid userId, ExternalIdentity identity, CancellationToken cancellationToken = default);

        Task UnbindAsync(Guid userId, ExternalAuthProvider provider, CancellationToken cancellationToken = default);
    }
}
