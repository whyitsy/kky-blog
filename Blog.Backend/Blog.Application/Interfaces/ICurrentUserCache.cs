using Blog.Domain.Entities;

namespace Blog.Application.Interfaces
{
    /// <summary>
    /// 认证校验所需的账号快照（**不是 EF 实体**）。
    ///
    /// <para><b>为什么必须是独立类型，不能直接缓存 User 实体：</b></para>
    /// <c>IUserRepository.GetByIdAsync</c> 返回的是**被 DbContext 跟踪**的实体。
    /// 把它塞进缓存并在后续请求中取出，会有两个真实风险：
    ///   1. 同一个实体实例被多个 DbContext 跟踪 → 抛
    ///      "The instance of entity type cannot be tracked because another instance
    ///       with the same key is already being tracked"
    ///   2. 更隐蔽的：它带着原 DbContext 的变更跟踪状态，任何对其属性的读写都可能
    ///      在**另一个请求**里被意外保存回数据库
    ///
    /// 所以缓存里只放这份不可变快照 —— 认证只需要这 5 个字段，一个不多。
    /// </summary>
    public sealed record CachedUserAuth(
        Guid UserId,
        UserRole Role,
        Guid? AuthorId,
        bool IsActive,
        int TokenVersion);

    /// <summary>
    /// 认证校验专用的账号读取（带缓存）。
    ///
    /// <para><b>为什么单独一个接口，而不是直接用 IUserRepository：</b></para>
    /// <c>CurrentUserResolutionMiddleware</c> 每个请求都要读一次账号，只为校验
    /// 「存在 / IsActive / TokenVersion」。若直连仓储，等于**每个请求都打一次数据库**，
    /// 而这几个字段的变化频率极低（只在改密 / 停用 / 注销时变）。
    ///
    /// 独立接口而不是「在仓储上加缓存」，是为了让缓存的适用范围**一眼可见且最小**：
    /// 别的仓储方法（如 GetByEmailAsync）保持直查，不会被顺手牵连。
    ///
    /// <para><b>一致性策略：</b></para>
    /// TTL（5 分钟）只是兜底；真正的正确性由**写路径主动失效**保证
    /// （见 <see cref="InvalidateAsync"/>，由 AuthService / UserService 在
    /// 改密 / 停用 / 注销后调用）。快照里的 TokenVersion 就是被失效的那个值。
    /// </summary>
    public interface ICurrentUserCache
    {
        /// <summary>
        /// 读取账号认证快照（命中缓存则不打数据库）。账号不存在时返回 null。
        /// </summary>
        Task<CachedUserAuth?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 失效某个账号的缓存。**必须在 TokenVersion 变更并提交之后调用**，
        /// 否则被踢下线的旧 token 还能继续用到 TTL 到期。
        /// </summary>
        Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
