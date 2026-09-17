using Blog.Application.Interfaces;
using Blog.Domain.IRepository;

namespace Blog.Infrastructure.Security
{
    /// <summary>
    /// <see cref="ICurrentUserCache"/> 的默认实现：把账号认证快照放进
    /// <see cref="ICacheService"/>（Memory 或 Redis，由 Cache:Provider 决定）。
    ///
    /// <para><b>为什么值得缓存</b></para>
    /// <c>CurrentUserResolutionMiddleware</c> 对**每一个带 token 的请求**都要读账号
    /// （校验存在性 / IsActive / TokenVersion）。在后台管理页里，一次操作会并发若干个
    /// 请求（列表 + 分类 + 标签 + 站点配置），于是同一个账号被重复查了多次 ——
    /// 这是纯粹的重复读，且这几个字段几乎不变。
    ///
    /// <para><b>失效路径</b></para>
    /// 所有会提升 TokenVersion 的写操作（注销 / 重置密码 / 停用账号）在**提交之后**
    /// 调用 <see cref="InvalidateAsync"/>。TTL 5 分钟只是「万一忘了失效」的兜底。
    /// 用固定 TTL 而不是绝对过期：本项目更怕「被踢下线的人还能用」，宁可多吃一次回源。
    /// </summary>
    public sealed class CurrentUserCache : ICurrentUserCache
    {
        /// <summary>
        /// 缓存有效期。取 5 分钟的依据：
        ///   - 它是「失效失败时的最大暴露窗口」，越短越安全
        ///   - 后台页面一次会话通常几分钟，5 分钟已能吃掉绝大多数重复读
        ///   - 不需要更长：账号认证信息的变化都是主动失效的，TTL 只承担兜底
        /// </summary>
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

        private readonly IUserRepository _users;
        private readonly ICacheService _cache;

        public CurrentUserCache(IUserRepository users, ICacheService cache)
        {
            _users = users;
            _cache = cache;
        }

        public async Task<CachedUserAuth?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var key = CacheKeys.UserById(userId);

            // cacheNull: false —— 「账号不存在」不能写空哨兵。
            // 否则一个刚被软删除的账号会在 NullTtl（默认 2 分钟）内持续命中「不存在」，
            // 或在重建账号后继续被判为不存在。这里宁可多回源一次。
            return await _cache.GetOrCreateAsync(
                key,
                async ct =>
                {
                    var user = await _users.GetByIdAsync(userId, ct);
                    if (user is null) return null;

                    // 只取认证需要的字段，绝不把被跟踪的实体放进缓存（见 CachedUserAuth 的说明）
                    return new CachedUserAuth(user.Id, user.Role, user.AuthorId, user.IsActive, user.TokenVersion);
                },
                Ttl,
                cacheNull: false,
                cancellationToken);
        }

        public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default) =>
            _cache.RemoveAsync(CacheKeys.UserById(userId), cancellationToken);
    }
}
