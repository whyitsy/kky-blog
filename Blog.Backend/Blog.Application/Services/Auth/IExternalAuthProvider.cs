using Blog.Domain.Entities;

namespace Blog.Application.Services.Auth
{
    /// <summary>
    /// 第三方登录 provider 适配器。
    ///
    /// 每个 provider 只负责两件事：拼授权 URL、用 code 换回「平台用户 Id」。
    /// 绑定关系、state 校验、本站会话签发全部由 <see cref="IExternalAuthService"/> 统一处理。
    /// </summary>
    public interface IExternalAuthProvider
    {
        ExternalAuthProvider Provider { get; }

        /// <summary>配置中是否启用</summary>
        bool IsEnabled { get; }

        /// <summary>是否支持内嵌二维码登录（目前只有飞书）</summary>
        bool SupportsQr { get; }

        string BuildAuthorizationUrl(ExternalAuthAuthorizationRequest request);

        Task<ExternalIdentity> ExchangeCodeAsync(
            ExternalAuthCodeRequest request,
            CancellationToken cancellationToken = default);
    }
}
