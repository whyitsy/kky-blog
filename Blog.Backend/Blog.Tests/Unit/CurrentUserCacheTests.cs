using Blog.Application.Interfaces;
using Blog.Domain.Entities;
using Blog.Domain.IRepository;
using Blog.Infrastructure.Caching;
using Blog.Infrastructure.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Blog.Tests.Unit;

/// <summary>
/// <see cref="CurrentUserCache"/> 的单元测试（问题 3：TokenVersion 缓存的正确性）。
///
/// 这一组守的是**正确性**，不是性能：
///   - 命中缓存时不能回源（否则这个缓存等于没加）
///   - 账号不存在时**不能**写空哨兵（否则新账号会被误判为不存在）
///   - 失效必须立即生效（否则「注销/停用/改密后旧 token 还能用」，安全性直接崩）
///
/// 用真实的 <see cref="MemoryCacheService"/> 而不是 mock 缓存，
/// 这样哨兵值、TTL、失效这些**实现细节**也被一起验证到。
/// </summary>
public sealed class CurrentUserCacheTests
{
    private static (CurrentUserCache Cache, Mock<IUserRepository> Users) Create()
    {
        var users = new Mock<IUserRepository>();
        var memory = new MemoryCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CacheOptions()),
            NullLogger<MemoryCacheService>.Instance);

        return (new CurrentUserCache(users.Object, memory), users);
    }

    private static User CreateUser(int tokenVersion = 1, bool isActive = true)
    {
        var user = new User("cache-test@example.com", "hash", UserRole.Author, null);

        if (!isActive) user.SetActive(false);

        // 把 TokenVersion 推到指定值（领域方法每次只 +1）
        while (user.TokenVersion < tokenVersion)
            user.LogoutAllDevices();

        return user;
    }

    [Fact]
    public async Task 同一账号连续读取_只回源一次()
    {
        var (cache, users) = Create();
        var user = CreateUser();
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var first = await cache.GetAsync(user.Id);
        var second = await cache.GetAsync(user.Id);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(user.Id, second!.UserId);
        Assert.Equal(user.TokenVersion, second.TokenVersion);

        // 核心断言：第二次不该再打数据库
        users.Verify(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task 失效后再次读取_必须回源并拿到新的TokenVersion()
    {
        var (cache, users) = Create();
        var user = CreateUser(tokenVersion: 3);
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var before = await cache.GetAsync(user.Id);
        Assert.Equal(3, before!.TokenVersion);

        // 模拟「注销」：领域方法提升 TokenVersion，随后失效缓存
        user.LogoutAllDevices();
        Assert.Equal(4, user.TokenVersion);
        await cache.InvalidateAsync(user.Id);

        var after = await cache.GetAsync(user.Id);

        Assert.Equal(4, after!.TokenVersion);
        users.Verify(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// 账号不存在时返回 null，且**不写空哨兵**。
    /// 否则 NullTtl（默认 2 分钟）内即使账号被创建出来也会被判为不存在。
    /// </summary>
    [Fact]
    public async Task 账号不存在_返回null且不缓存空结果()
    {
        var (cache, users) = Create();
        var missingId = Guid.NewGuid();

        users.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
             .ReturnsAsync((User?)null);

        Assert.Null(await cache.GetAsync(missingId));
        Assert.Null(await cache.GetAsync(missingId));

        // 两次都应回源：说明没有把「不存在」缓存下来
        users.Verify(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>停用的账号也要能被读到（让中间件去判 Inactive 并给 401），不能因为缓存丢了状态</summary>
    [Fact]
    public async Task 停用账号_快照带回_IsActive为false()
    {
        var (cache, users) = Create();
        var user = CreateUser(isActive: false);
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var snapshot = await cache.GetAsync(user.Id);

        Assert.NotNull(snapshot);
        Assert.False(snapshot!.IsActive);
    }

    /// <summary>
    /// 缓存里放的必须是**快照**而不是被跟踪的实体。
    /// 这里断言返回类型不带实体引用（类型层面已由签名保证），
    /// 并确认字段值被正确复制 —— 防止有人日后「优化」成直接缓存 User。
    /// </summary>
    [Fact]
    public async Task 缓存的是快照而非实体()
    {
        var (cache, users) = Create();
        var user = CreateUser(tokenVersion: 7);
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var snapshot = await cache.GetAsync(user.Id);

        Assert.IsType<CachedUserAuth>(snapshot);
        Assert.Equal(user.Role, snapshot!.Role);
        Assert.Equal(user.AuthorId, snapshot.AuthorId);
        Assert.Equal(user.TokenVersion, snapshot.TokenVersion);

        // 快照必须与实体脱钩：改实体后再取缓存，拿到的仍是旧快照（证明没有共享引用）
        user.LogoutAllDevices();
        var again = await cache.GetAsync(user.Id);
        Assert.Equal(7, again!.TokenVersion);
    }
}
