using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Blog.Domain.IRepository;
using Moq;

namespace Blog.Tests.Unit;

/// <summary>
/// UserService 的**单元测试**：只测「不与数据库/HTTP 绑定的业务规则」。
///
/// 分工说明（与 PostServiceTests 一致）：
///   - 权限矩阵、状态码 401/403 等 HTTP 行为 → 集成测试（见 Integration/）。
///   - 这里测的是服务层的编排是否把**领域不变量**触发到位，尤其是
///     「停用账号必须让已签发的 token 立即作废」这一条。
///
/// 这组测试守的是一个具体的缺陷：<c>PUT /api/users/{id}</c> 停用账号时，
/// <c>UpdateProfile</c> 先把 <c>IsActive</c> 写成目标值，随后的 <c>SetActive</c>
/// 因「值已经相等」而提前返回，于是 <c>TokenVersion</c> 没有被提升 ——
/// 账号虽然当场被踢下线（靠缓存失效），但**重新启用后旧 token 会复活**。
/// </summary>
public sealed class UserServiceTests
{
    [Fact]
    public async Task 通过更新接口停用账号_应提升TokenVersion()
    {
        var (service, mocks, user) = Create();
        Assert.Equal(1, user.TokenVersion);

        await service.UpdateAsync(user.Id, new UpdateUserRequest("Author", null, IsActive: false, Version: 1));

        Assert.False(user.IsActive);
        // TokenVersion 必须 +1：否则「停用」只作用于当前会话，
        // 账号一旦被重新启用，停用前签发的 token 又会通过校验。
        Assert.Equal(2, user.TokenVersion);
        mocks.CurrentUserCache.Verify(
            c => c.InvalidateAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task 通过更新接口重新启用账号_不应再提升TokenVersion()
    {
        // 已是停用状态（TokenVersion 已经被停用动作推到了 2）
        var (service, _, user) = Create(isActive: false);
        Assert.Equal(2, user.TokenVersion);

        await service.UpdateAsync(user.Id, new UpdateUserRequest("Author", null, IsActive: true, Version: 1));

        Assert.True(user.IsActive);
        // 启用**不是**作废动作：既不该再 +1，也不该把旧 token 变回有效
        Assert.Equal(2, user.TokenVersion);
    }

    [Fact]
    public async Task 更新角色但不改启用状态_不应提升TokenVersion()
    {
        // 幂等性守卫：管理员只是改个角色/署名归属，不该把该账号的所有会话踢掉。
        // （如果哪天有人为了修上面那个缺陷而让 SetActive 无条件 +1，这条会立刻红）
        var (service, _, user) = Create();

        await service.UpdateAsync(user.Id, new UpdateUserRequest("Admin", null, IsActive: true, Version: 1));

        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(1, user.TokenVersion);
    }

    // ---------------------------------------------------------------- 辅助

    private sealed record Mocks(
        Mock<IUserRepository> Users,
        Mock<IAuthorRepository> Authors,
        Mock<IPasswordHasher> PasswordHasher,
        Mock<ICurrentUserCache> CurrentUserCache,
        Mock<IUnitOfWork> Uow);

    private static (UserService Service, Mocks Mocks, User User) Create(bool isActive = true)
    {
        var user = new User("user-service-test@example.com", "hash", UserRole.Author)
        {
            Id = Guid.NewGuid(),
        };

        if (!isActive)
            user.SetActive(false); // 走到 TokenVersion = 2，与「先被停用过」的状态一致

        var mocks = new Mocks(
            new Mock<IUserRepository>(),
            new Mock<IAuthorRepository>(),
            new Mock<IPasswordHasher>(),
            new Mock<ICurrentUserCache>(),
            new Mock<IUnitOfWork>());

        mocks.Users
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mocks.Uow
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        mocks.CurrentUserCache
            .Setup(c => c.InvalidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new UserService(
            mocks.Users.Object,
            mocks.Authors.Object,
            mocks.PasswordHasher.Object,
            mocks.CurrentUserCache.Object,
            mocks.Uow.Object);

        return (service, mocks, user);
    }
}
