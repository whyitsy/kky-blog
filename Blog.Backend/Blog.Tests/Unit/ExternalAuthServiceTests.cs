using Blog.Application.Common;
using Blog.Application.Common.Exceptions;
using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Blog.Domain.IRepository;
using Moq;

namespace Blog.Tests.Unit;

/// <summary>
/// ExternalAuthService 的核心规则：
///   - 只允许绑定已有账号，不自动注册；
///   - state / 租户 / 账号状态必须校验；
///   - 只存 Provider + ProviderUserId + UserId。
/// </summary>
public sealed class ExternalAuthServiceTests
{
    [Fact]
    public async Task StartAsync_启用Provider_返回授权URL和State()
    {
        var provider = new FakeProvider
        {
            Provider = ExternalAuthProvider.GitHub,
            IsEnabled = true,
            AuthorizationUrl = "https://github.com/login/oauth/authorize?state=abc",
        };
        var service = Create(providers: [provider]);

        var result = await service.StartAsync(
            "github",
            ExternalAuthIntent.Login,
            ExternalAuthFlow.Redirect,
            "/admin/posts",
            bindUserId: null);

        Assert.Equal(provider.AuthorizationUrl, result.AuthorizationUrl);
        Assert.Equal(ExternalAuthProvider.GitHub, result.State.Provider);
        Assert.Equal(ExternalAuthIntent.Login, result.State.Intent);
        Assert.Equal(ExternalAuthFlow.Redirect, result.State.Flow);
        Assert.Equal("/admin/posts", result.State.ReturnUrl);
        Assert.False(string.IsNullOrWhiteSpace(result.State.State));
        Assert.True(result.State.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task StartAsync_禁用Provider_抛NotFound()
    {
        var provider = new FakeProvider
        {
            Provider = ExternalAuthProvider.GitHub,
            IsEnabled = false,
        };
        var service = Create(providers: [provider]);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.StartAsync("github", ExternalAuthIntent.Login, ExternalAuthFlow.Redirect, null, null));

        Assert.Equal(ErrorCodes.NotFound, ex.Code);
    }

    [Fact]
    public async Task StartAsync_非法ReturnUrl_抛InvalidArgument()
    {
        var provider = new FakeProvider { Provider = ExternalAuthProvider.GitHub, IsEnabled = true };
        var service = Create(providers: [provider]);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.StartAsync("github", ExternalAuthIntent.Login, ExternalAuthFlow.Redirect, "https://evil.example", null));

        Assert.Equal(ErrorCodes.InvalidArgument, ex.Code);
    }

    [Fact]
    public async Task HandleCallback_未绑定_返回Unbound()
    {
        var provider = new FakeProvider
        {
            Provider = ExternalAuthProvider.GitHub,
            IsEnabled = true,
            Identity = new ExternalIdentity(ExternalAuthProvider.GitHub, "12345678"),
        };
        var externalLogins = new Mock<IExternalLoginRepository>();
        externalLogins
            .Setup(r => r.GetByProviderUserAsync(ExternalAuthProvider.GitHub, "12345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        var service = Create(providers: [provider], externalLogins: externalLogins);

        var result = await service.HandleCallbackAsync(
            "github", "code", "state-1", null, State("state-1"));

        Assert.Equal(ExternalAuthCallbackOutcome.Unbound, result.Outcome);
        Assert.Equal("external_unbound", result.Error);
    }

    [Fact]
    public async Task HandleCallback_已绑定且账号启用_返回LoginSucceeded并记录登录时间()
    {
        var user = NewUser();
        var binding = NewBinding(user.Id);
        var provider = new FakeProvider
        {
            Provider = ExternalAuthProvider.GitHub,
            IsEnabled = true,
            Identity = new ExternalIdentity(ExternalAuthProvider.GitHub, binding.ProviderUserId),
        };
        var externalLogins = new Mock<IExternalLoginRepository>();
        externalLogins
            .Setup(r => r.GetByProviderUserAsync(ExternalAuthProvider.GitHub, binding.ProviderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var uow = new Mock<IUnitOfWork>();
        var service = Create(providers: [provider], externalLogins: externalLogins, users: users, uow: uow);

        var result = await service.HandleCallbackAsync(
            "github", "code", "state-1", null, State("state-1"));

        Assert.Equal(ExternalAuthCallbackOutcome.LoginSucceeded, result.Outcome);
        Assert.Equal(user.Id, result.UserId);
        Assert.NotNull(binding.LastLoginAt);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleCallback_State不一致_返回Failed()
    {
        var provider = new FakeProvider { Provider = ExternalAuthProvider.GitHub, IsEnabled = true };
        var service = Create(providers: [provider]);

        var result = await service.HandleCallbackAsync(
            "github", "code", "wrong-state", null, State("state-1"));

        Assert.Equal(ExternalAuthCallbackOutcome.Failed, result.Outcome);
        Assert.Equal("invalid_state", result.Error);
    }

    [Fact]
    public async Task HandleCallback_飞书租户不匹配_返回Failed()
    {
        var options = new ExternalAuthOptions
        {
            Feishu = new FeishuExternalAuthOptions { Enabled = true, TenantKey = "tenant-a" },
        };
        var provider = new FakeProvider
        {
            Provider = ExternalAuthProvider.Feishu,
            IsEnabled = true,
            Identity = new ExternalIdentity(ExternalAuthProvider.Feishu, "ou_1", "tenant-b"),
        };
        var service = Create(providers: [provider], options: options);

        var result = await service.HandleCallbackAsync(
            "feishu", "code", "state-1", null, State("state-1", ExternalAuthProvider.Feishu));

        Assert.Equal(ExternalAuthCallbackOutcome.Failed, result.Outcome);
        Assert.Equal("tenant_mismatch", result.Error);
    }

    [Fact]
    public async Task BindAsync_身份已绑定其他账号_抛DuplicateResource()
    {
        var current = NewUser();
        var other = NewUser();
        var existing = NewBinding(other.Id);
        var externalLogins = new Mock<IExternalLoginRepository>();
        externalLogins
            .Setup(r => r.GetByProviderUserAsync(ExternalAuthProvider.GitHub, existing.ProviderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(current.Id, It.IsAny<CancellationToken>())).ReturnsAsync(current);
        var service = Create(externalLogins: externalLogins, users: users);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.BindAsync(current.Id, new ExternalIdentity(ExternalAuthProvider.GitHub, existing.ProviderUserId)));

        Assert.Equal(ErrorCodes.DuplicateResource, ex.Code);
    }

    [Fact]
    public async Task CompleteLogin_账号停用_抛Unauthorized()
    {
        var user = NewUser();
        user.SetActive(false);
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var service = Create(users: users);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CompleteLoginAsync(user.Id));

        Assert.Equal(ErrorCodes.Unauthorized, ex.Code);
    }

    // ---------------------------------------------------------------- 辅助

    private static User NewUser()
    {
        var user = new User("external-auth-test@example.com", "hash", UserRole.Admin)
        {
            Id = Guid.NewGuid(),
        };
        return user;
    }

    private static ExternalLogin NewBinding(Guid userId) =>
        new(userId, ExternalAuthProvider.GitHub, $"gh-{Guid.NewGuid():N}");

    private static ExternalAuthState State(
        string state,
        ExternalAuthProvider provider = ExternalAuthProvider.GitHub,
        ExternalAuthIntent intent = ExternalAuthIntent.Login,
        string? returnUrl = "/") =>
        new(provider, intent, ExternalAuthFlow.Redirect, state, null, returnUrl, null,
            DateTimeOffset.UtcNow.AddMinutes(5));

    private static ExternalAuthService Create(
        IEnumerable<IExternalAuthProvider>? providers = null,
        Mock<IExternalLoginRepository>? externalLogins = null,
        Mock<IUserRepository>? users = null,
        Mock<IAuthorRepository>? authors = null,
        Mock<ITokenService>? tokenService = null,
        Mock<IUnitOfWork>? uow = null,
        ExternalAuthOptions? options = null)
    {
        externalLogins ??= new Mock<IExternalLoginRepository>();
        users ??= new Mock<IUserRepository>();
        authors ??= new Mock<IAuthorRepository>();
        tokenService ??= new Mock<ITokenService>();
        uow ??= new Mock<IUnitOfWork>();
        options ??= new ExternalAuthOptions();

        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tokenService
            .Setup(t => t.Issue(It.IsAny<User>()))
            .Returns(("token", DateTimeOffset.UtcNow.AddMinutes(30)));

        return new ExternalAuthService(
            providers ?? [],
            externalLogins.Object,
            users.Object,
            authors.Object,
            tokenService.Object,
            uow.Object,
            options);
    }

    private sealed class FakeProvider : IExternalAuthProvider
    {
        public ExternalAuthProvider Provider { get; init; }
        public bool IsEnabled { get; init; }
        public bool SupportsQr { get; init; }
        public string AuthorizationUrl { get; init; } = "https://provider.example/authorize";
        public ExternalIdentity Identity { get; init; } =
            new(ExternalAuthProvider.GitHub, "provider-user");

        public string BuildAuthorizationUrl(ExternalAuthAuthorizationRequest request) => AuthorizationUrl;

        public Task<ExternalIdentity> ExchangeCodeAsync(
            ExternalAuthCodeRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Identity);
    }
}
