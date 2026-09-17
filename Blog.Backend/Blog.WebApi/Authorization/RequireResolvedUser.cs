using Blog.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Blog.WebApi.Authorization;

/// <summary>
/// 要求「当前用户已被成功解析」——即 <c>CurrentUserResolutionMiddleware</c> 把
/// <see cref="Blog.Infrastructure.Security.ResolvedUser"/> 写进了 <c>HttpContext.Items</c>。
///
/// <para><b>为什么需要它（问题 6）：</b></para>
/// <c>[Authorize]</c> 只校验 JWT 自身的签名与有效期。而 JWT 是**无状态**的，
/// 改密 / 停用 / 注销之后旧 token 在到期前依然「有效」—— 项目的补偿方案是
/// <c>CurrentUserResolutionMiddleware</c> 比对 claim 里的 <c>tv</c> 与库中 <c>TokenVersion</c>。
/// 但该中间件在校验失败时**刻意不返回 401**，只是不写入当前用户
/// （为了让公开接口带一个过期 token 也能正常浏览）。
///
/// 结果是一个危险的空档：一个**已被作废**的 token 在 <c>[Authorize]</c> 看来仍然合法，
/// 端点于是拿着「解析结果为空」的身份继续执行。实测（修复前）：
/// <c>GET /api/posts/{已发布id}/readonly</c> 在注销后仍返回 <c>200</c> + 文章数据。
///
/// 这个 requirement 把那条空档补上：**凡是标了 <c>[Authorize]</c> 的端点，
/// 都要求身份真的被解析成功**；token 被作废即 401，而不是静默降级成匿名后
/// 由各个业务分支各自兜底（那样必然漏，且漏得没有声音）。
///
/// <para><b>为什么不直接在中间件里返回 401：</b></para>
/// 那会让「公开端点 + 过期 token」也变成 401，违背中间件不返回 401 的原始意图。
/// 把判断放在**授权阶段**，语义才精确：只有要求身份的端点才拒绝。
/// </summary>
public sealed class RequireResolvedUserRequirement : IAuthorizationRequirement;

/// <summary>
/// <see cref="RequireResolvedUserRequirement"/> 的处理器。
/// 只读 <see cref="ICurrentUser.IsAuthenticated"/> —— 它背后就是中间件写入的解析结果，
/// 因此这里不碰 HttpContext，也天然可用单元测试覆盖。
/// </summary>
public sealed class RequireResolvedUserHandler : AuthorizationHandler<RequireResolvedUserRequirement>
{
    private readonly ICurrentUser _currentUser;

    public RequireResolvedUserHandler(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireResolvedUserRequirement requirement)
    {
        if (_currentUser.IsAuthenticated)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 身份无效 -> 明确失败，并把原因带出去（仅作排障用）。
        //
        // 为什么必须显式 Fail：授权失败时 PolicyEvaluator 会二选一 ——
        // Challenge（401）还是 Forbid（403）。它默认看「用户是否已认证」，而这里
        // JWT 本身签名有效、未过期，所以框架认为「已认证」，于是选 Forbid -> 403。
        // 但「注销后旧 token」语义上是**凭据无效**，应当是 401。
        //
        // 401 / 403 的最终判定不依赖这里传出的字符串，而是由 JwtBearerEvents.OnForbidden
        // 读 CurrentUserResolutionMiddleware.RejectionKey 得出（见 Program.cs）——
        // 那个 Items 才是「身份为什么被拒」的唯一事实来源。
        context.Fail(new AuthorizationFailureReason(this,
            _currentUser.CredentialRejection?.ToString() ?? "未解析到登录用户"));
        return Task.CompletedTask;
    }
}
