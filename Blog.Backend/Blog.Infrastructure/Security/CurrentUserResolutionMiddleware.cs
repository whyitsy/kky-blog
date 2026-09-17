using Blog.Application.Interfaces;
using Blog.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace Blog.Infrastructure.Security
{
    /// <summary>每个请求解析出的登录用户信息（由 <see cref="CurrentUserResolutionMiddleware"/> 写入）</summary>
    public sealed record ResolvedUser(Guid UserId, UserRole Role, Guid? AuthorId);

    /// <summary>
    /// 在当前请求内解析并校验登录用户，结果放入 <see cref="HttpContext.Items"/>，
    /// 供 <see cref="HttpCurrentUser"/> 同步读取，避免在属性 getter 里同步阻塞异步查询。
    ///
    /// 校验内容（见 docs/02-架构与数据模型.md §10.3）：
    ///   1. JWT 签名与过期时间 —— 由 JwtBearer 中间件完成
    ///   2. 账号存在且未被软删除
    ///   3. 账号处于启用状态
    ///   4. claim 中的 TokenVersion 与数据库一致（改密 / 停用 / 注销后旧 token 立即失效）
    ///
    /// 任一条不满足即视为**未认证**：不在此处直接返回 401，
    /// 而是让后续的 [Authorize] 去拒绝，这样公开接口带一个过期 token 时仍能正常访问。
    /// 被拒绝的原因写入 <see cref="RejectionKey"/>（<see cref="CredentialRejectionReason"/>），
    /// 供授权阶段区分「凭据无效」(401) 与「权限不足」(403)。
    /// </summary>
    public sealed class CurrentUserResolutionMiddleware
    {
        public const string ItemsKey = "__blog_current_user";

        /// <summary>本次请求身份被拒的原因（<see cref="CredentialRejectionReason"/>），仅在被拒时写入</summary>
        public const string RejectionKey = "__blog_credential_rejection";

        private readonly RequestDelegate _next;

        public CurrentUserResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITokenService tokenService, ICurrentUserCache users)
        {
            var info = tokenService.Resolve(context.User);
            if (info is not null)
            {
                // 走缓存（TTL 5 分钟，TokenVersion 变更时主动失效）。
                // 这里刻意不用 IUserRepository 直查：每个带 token 的请求都要读一次账号，
                // 而后台一个页面会并发好几个请求 —— 那是纯粹的重复读（见 ICurrentUserCache）。
                var user = await users.GetAsync(info.UserId, context.RequestAborted);

                if (user is null)
                    context.Items[RejectionKey] = CredentialRejectionReason.UserNotFound;
                else if (!user.IsActive)
                    context.Items[RejectionKey] = CredentialRejectionReason.Inactive;
                else if (user.TokenVersion != info.TokenVersion)
                    context.Items[RejectionKey] = CredentialRejectionReason.TokenVersionMismatch;
                else
                    context.Items[ItemsKey] = new ResolvedUser(user.UserId, user.Role, user.AuthorId);
            }

            await _next(context);
        }
    }
}
