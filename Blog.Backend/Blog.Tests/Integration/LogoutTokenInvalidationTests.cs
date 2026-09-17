using System.Net;
using Blog.Tests.Infrastructure;

namespace Blog.Tests.Integration;

/// <summary>
/// 登录态失效（TokenVersion 作废）后的访问控制回归测试。
///
/// 背景（问题 6）：<c>CurrentUserResolutionMiddleware</c> 发现 claim 里的 <c>tv</c>
/// 与库中不一致时，会**刻意不写入**当前用户（设计如此：让公开接口带一个过期 token
/// 仍能看首页）。但 <c>[Authorize]</c> 只校验 JWT 自身的签名与有效期 ——
/// JWT 还没到期时它照样通过，于是端点拿着一个「已被作废的身份」继续跑。
///
/// 实测表现（修复前）：
///   - <c>/api/posts/{草稿id}</c>            → 404（被 CanReadUnpublished 拦住，但靠的是 null 比较的巧合）
///   - <c>/api/posts/{已发布id}/readonly</c> → **200 + 数据**（草稿分支根本不执行，直接放行）
///
/// 修复方向：让「token 被作废」在授权阶段就表现为 401
/// （<see cref="Blog.WebApi.Authorization.RequireResolvedUserRequirement"/>），
/// 而不是静默降级后由各个业务分支各自兜底。
///
/// <para><b>⚠️ 测试隔离约定</b></para>
/// 整个测试集合共用一个库与一个应用实例，而 <c>LogoutAsync</c> 会**永久**提升账号的
/// TokenVersion。因此本组测试**绝不能拿种子 admin 去注销** —— 那会让后续所有依赖
/// admin token 的测试全部 401（实测踩过：52 个用例连环失败）。
/// 这里每个用例都新建一个一次性 Author 账号，只拿它注销。
/// </summary>
[Collection(BlogApiCollection.Name)]
public sealed class LogoutTokenInvalidationTests
{
    private readonly ApiClient _api;

    public LogoutTokenInvalidationTests(BlogApiFixture fixture)
    {
        _api = new ApiClient(fixture.CreateClient());
    }

    // ---------------------------------------------------------------- 被作废的 token

    /// <summary>注销后，旧 token 访问**需要登录**的端点一律 401（而不是静默降级/返回数据）</summary>
    [Fact]
    public async Task 注销后_旧token访问readonly端点_应401()
    {
        var admin = await LoginAdminAsync();
        var published = await CreatePostAsync(admin, publish: true);

        // 用一次性账号验证端点本身可用（注销前的对照）
        var token = await LoginFreshAuthorAsync();
        var (beforeStatus, _, _, _) = await _api.CallAsync<PostDetail>(
            HttpMethod.Get, $"/api/posts/{published.Id}/readonly", token);
        Assert.Equal(HttpStatusCode.OK, beforeStatus);

        await LogoutAsync(token);

        var (status, code, data, message) = await _api.CallAsync<PostDetail>(
            HttpMethod.Get, $"/api/posts/{published.Id}/readonly", token);

        Assert.True(status == HttpStatusCode.Unauthorized,
            $"作废 token 访问 /readonly 应 401，实际 {status} / code={code} / message={message} / 是否拿到数据={data is not null}");
        Assert.Equal(Codes.Unauthorized, code);
    }

    /// <summary>注销后，旧 token 访问「读取当前用户」的端点应 401</summary>
    [Fact]
    public async Task 注销后_旧token访问me_应401()
    {
        var token = await LoginFreshAuthorAsync();
        await LogoutAsync(token);

        var (status, code, _, _) = await _api.CallAsync<CurrentUserDto>(
            HttpMethod.Get, "/api/auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal(Codes.Unauthorized, code);
    }

    /// <summary>注销后，旧 token 的写操作应 401（而不是 403 或 500）</summary>
    [Fact]
    public async Task 注销后_旧token写操作_应401()
    {
        var token = await LoginFreshAuthorAsync();
        await LogoutAsync(token);

        var (status, code, _, _) = await _api.CallAsync<object>(
            HttpMethod.Post, "/api/tags", token, new { name = $"注销后不应创建成功-{Guid.NewGuid():N}" });

        Assert.True(status == HttpStatusCode.Unauthorized,
            $"作废 token 的写操作应 401，实际 {status} / code={code}");
        Assert.Equal(Codes.Unauthorized, code);
    }

    /// <summary>注销后，旧 token 读**草稿**的 readonly 端点也应 401（身份无效优先于资源不存在）</summary>
    [Fact]
    public async Task 注销后_旧token读草稿readonly端点_仍应401()
    {
        var token = await LoginFreshAuthorAsync();
        var draft = await CreatePostAsync(token, publish: false);

        await LogoutAsync(token);

        var (status, code, _, _) = await _api.CallAsync<PostDetail>(
            HttpMethod.Get, $"/api/posts/{draft.Id}/readonly", token);

        Assert.True(status == HttpStatusCode.Unauthorized,
            $"作废 token 访问 /readonly 应 401，实际 {status} / code={code}");
    }

    // ---------------------------------------------------------------- 对照：合法 token 不受影响

    /// <summary>合法 token 访问 /readonly 必须继续正常（防止修复过度）</summary>
    [Fact]
    public async Task 未注销的token_访问readonly端点_应200()
    {
        var token = await LoginFreshAuthorAsync();
        var published = await CreatePostAsync(
            await LoginAdminAsync(), publish: true);

        var (status, code, data, _) = await _api.CallAsync<PostDetail>(
            HttpMethod.Get, $"/api/posts/{published.Id}/readonly", token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Codes.Ok, code);
        Assert.Equal(published.Id, data!.Id);
    }

    /// <summary>
    /// 公开接口带一个**已作废**的 token 仍应可访问 —— 这是中间件「不直接返回 401」的原始设计意图，
    /// 修复后必须继续成立（否则一个过期 token 会让用户连首页都打不开）。
    /// </summary>
    [Fact]
    public async Task 注销后_旧token访问公开接口_仍应200()
    {
        var token = await LoginFreshAuthorAsync();
        var admin = await LoginAdminAsync();
        var published = await CreatePostAsync(admin, publish: true);

        await LogoutAsync(token);

        var (status, code, data, _) = await _api.CallAsync<PostDetail>(
            HttpMethod.Get, $"/api/posts/{published.Id}", token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Codes.Ok, code);
        Assert.Equal(published.Id, data!.Id);
    }

    /// <summary>匿名（完全不带 token）访问公开接口不受影响</summary>
    [Fact]
    public async Task 匿名_访问公开接口_应200()
    {
        var published = await CreatePostAsync(await LoginAdminAsync(), publish: true);

        var (status, code, _, _) = await _api.CallAsync<PostDetail>(
            HttpMethod.Get, $"/api/posts/{published.Id}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Codes.Ok, code);
    }

    // ---------------------------------------------------------------- 辅助

    private Task<string> LoginAdminAsync() => _api.LoginAsync("admin@example.com", "Admin@12345");

    /// <summary>
    /// 新建一个一次性 Author 账号并登录。
    /// **不要用种子 admin 做注销测试**：TokenVersion 的提升是永久的，会污染整个测试集合。
    /// </summary>
    private async Task<string> LoginFreshAuthorAsync()
    {
        var admin = await LoginAdminAsync();
        var email = $"logout-test-{Guid.NewGuid():N}@example.com";
        const string password = "Author@12345";

        var (status, code, _, message) = await _api.CallAsync<object>(
            HttpMethod.Post, "/api/users", admin,
            new { email, password, role = "Author", authorId = (Guid?)null });

        Assert.True(status == HttpStatusCode.OK && code == Codes.Ok,
            $"创建一次性测试账号失败 status={status} code={code} message={message}");

        return await _api.LoginAsync(email, password);
    }

    private async Task LogoutAsync(string token)
    {
        var (status, code, _, message) = await _api.CallAsync<object>(
            HttpMethod.Post, "/api/auth/logout", token);

        Assert.True(status == HttpStatusCode.OK && code == Codes.Ok,
            $"注销应成功，实际 {status}/{code} {message}");
    }

    private async Task<PostDetail> CreatePostAsync(string token, bool publish)
    {
        var (status, code, data, message) = await _api.CallAsync<PostDetail>(
            HttpMethod.Post, "/api/posts", token, new
            {
                title = $"失效测试-{Guid.NewGuid():N}",
                content = "正文",
                summary = (string?)null,
                coverImage = "",
                categoryId = (Guid?)null,
                tagIds = Array.Empty<Guid>(),
                collectionIds = Array.Empty<Guid>(),
                authorId = (Guid?)null,
                publish,
            });

        Assert.True(status == HttpStatusCode.OK && code == Codes.Ok && data is not null,
            $"创建文章失败 status={status} code={code} message={message}");

        return data!;
    }
}
