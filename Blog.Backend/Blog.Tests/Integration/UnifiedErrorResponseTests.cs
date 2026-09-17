using System.Net;
using System.Text.Json;
using Blog.Tests.Infrastructure;

namespace Blog.Tests.Integration;

/// <summary>
/// 「统一响应体」的边界测试。
///
/// <para><b>为什么需要这一组</b></para>
/// docs/02 §4.1 与 docs/03 §1.1 都承诺「<b>所有</b>接口（含错误）返回 {code,message,data}」。
/// 但 <c>[ApiController]</c> 的自动模型校验发生在 Action <b>之前</b>，
/// 默认输出 RFC 7807 <c>ProblemDetails</c>（{type,title,status,errors,traceId}）——
/// 于是同一个 API 并存两种错误结构。
///
/// <para><b>怎么发现的</b></para>
/// 手工验证全文检索时漏传 <c>keyword</c>，返回的是：
/// <code>{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"keyword":["The keyword field is required."]},...}</code>
///
/// <para><b>为什么断言"没有 type/title"而不只是"有 code"</b></para>
/// 只断言 <c>code == 4001</c> 的话，一个「两种结构都塞进去」的实现也能变绿。
/// 必须钉住<b>不含</b> ProblemDetails 的字段。
/// </summary>
[Collection(BlogApiCollection.Name)]
public sealed class UnifiedErrorResponseTests
{
    private readonly ApiClient _api;

    public UnifiedErrorResponseTests(BlogApiFixture fixture) => _api = new ApiClient(fixture.CreateClient());

    /// <summary>断言响应体是统一结构，且不是 ProblemDetails</summary>
    private static void AssertUnifiedErrorBody(string raw, int expectedCode)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("code", out var code), $"缺少 code 字段：{raw}");
        Assert.Equal(expectedCode, code.GetInt32());

        Assert.True(root.TryGetProperty("message", out var message), $"缺少 message 字段：{raw}");
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()), $"message 不能为空：{raw}");

        // 反面断言：ProblemDetails 的特征字段一个都不能出现
        Assert.False(root.TryGetProperty("type", out _), $"不应出现 ProblemDetails 的 type：{raw}");
        Assert.False(root.TryGetProperty("title", out _), $"不应出现 ProblemDetails 的 title：{raw}");
        Assert.False(root.TryGetProperty("errors", out _), $"不应出现 ProblemDetails 的 errors：{raw}");
        Assert.False(root.TryGetProperty("traceId", out _), $"不应出现 ProblemDetails 的 traceId：{raw}");
    }

    [Fact]
    public async Task 缺少必填查询参数_返回统一响应体()
    {
        // /api/posts/search 的 keyword 是必填的（PostsController.Search 的第一个参数无默认值）
        var res = await _api.SendAsync(HttpMethod.Get, "/api/posts/search");
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        AssertUnifiedErrorBody(raw, Codes.InvalidArgument);
        Assert.Contains("keyword", raw); // 提示里要能指出是哪个参数
    }

    [Fact]
    public async Task 请求体类型不匹配_返回统一响应体()
    {
        // 给一个"合法 JSON 但不是对象"的请求体：模型绑定会失败，走的是同一条工厂路径
        var res = await _api.SendAsync(HttpMethod.Post, "/api/auth/login", body: "this-is-not-an-object");
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        AssertUnifiedErrorBody(raw, Codes.InvalidArgument);
    }

    [Fact]
    public async Task 正常请求不受影响_仍返回统一成功体()
    {
        // 反向护栏：接管工厂不能把正常路径也改坏
        var res = await _api.SendAsync(HttpMethod.Get, "/api/site/config");
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(raw);
        Assert.Equal(Codes.Ok, doc.RootElement.GetProperty("code").GetInt32());
    }

    // ---------------------------------------------------------------- 路由阶段的失败（问题 1）

    /// <summary>
    /// 路由匹配阶段的失败也必须有统一响应体。
    ///
    /// <para><b>为什么这一组之前是空档</b></para>
    /// <c>InvalidModelStateResponseFactory</c> 只覆盖「进了 Action 之后的模型校验」。
    /// 而路由约束（<c>{id:guid}</c>）在**更早**的 Endpoint 选择阶段就把请求判死了，
    /// 走的是 ASP.NET Core 默认的空 404 —— 没有 Content-Type、没有 body。
    /// 前端 <c>http.ts</c> 只能靠 HTTP 状态码兜底，于是「所有接口都返回统一体」这句话
    /// 在路由阶段并不成立。
    ///
    /// <para><b>修复方式</b></para>
    /// <c>UseStatusCodePages</c> 全局兜底（Program.cs）：凡是状态码 ≥400 且**响应体为空**的响应，
    /// 一律补写成 <c>{code,message,data}</c>。刻意不引入 ProblemDetails ——
    /// 那会与本项目的 ApiResponse 并存成两套结构，正是这个测试要防的事。
    /// </summary>
    [Theory]
    // 路由约束不匹配：id 不是 GUID -> 该路由不匹配 -> 404
    [InlineData("/api/posts/not-a-guid")]
    [InlineData("/api/authors/not-a-guid")]
    [InlineData("/api/users/not-a-guid")]
    [InlineData("/api/collections/id/not-a-guid")]
    // 完全不存在的路径
    [InlineData("/api/totally/wrong/path")]
    [InlineData("/api/posts/not-a-guid/nope")]
    public async Task 路由无法匹配_返回统一响应体(string path)
    {
        var res = await _api.SendAsync(HttpMethod.Get, path);
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        Assert.False(string.IsNullOrWhiteSpace(raw),
            $"路由 404 不应是空响应体（path={path}）—— 客户端无法解析成 {{code,message,data}}");

        AssertUnifiedErrorBody(raw, Codes.NotFound);
    }

    /// <summary>
    /// 路由约束的「早拒绝」语义值得钉住：<c>{id:guid}</c> 在**路由匹配阶段**就不匹配非法 id，
    /// 因此请求根本走不到 <c>[Authorize]</c>，得到的是 404（而不是 401）。
    ///
    /// 这与「需登录的路径带非法 id」看起来矛盾，其实顺序很清晰：
    ///   路由约束不匹配 → 404（没有 Endpoint，谈不上认证/授权）
    ///   路由匹配但未认证 → 401
    ///   路由匹配、已认证但角色不足 → 403
    ///
    /// 也正因为约束在这么早的阶段拒绝，它产出的 404 原先没有任何 body ——
    /// 这正是 <c>UseStatusCodePages</c> 兜底要解决的问题。
    /// </summary>
    [Fact]
    public async Task 需登录的路径带非法id_路由约束先拒绝_返回404统一响应体()
    {
        var res = await _api.SendAsync(HttpMethod.Get, "/api/posts/not-a-guid/readonly");
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        AssertUnifiedErrorBody(raw, Codes.NotFound);
    }

    /// <summary>
    /// 对照：路由匹配 + 未认证时是 401（而不是 404）。
    /// 用来证明上一条的 404 确实来自路由约束，而不是「认证失败被伪装成 404」。
    /// </summary>
    [Fact]
    public async Task 需登录的路径_合法id但未认证_返回401统一响应体()
    {
        var res = await _api.SendAsync(
            HttpMethod.Get, $"/api/posts/{Guid.NewGuid()}/readonly");
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        AssertUnifiedErrorBody(raw, Codes.Unauthorized);
    }

    /// <summary>方法不允许（路径存在但 HTTP 方法不对）同样要有统一响应体</summary>
    [Theory]
    [InlineData("/api/posts")]
    [InlineData("/api/site/config")]
    public async Task 方法不允许_返回统一响应体(string path)
    {
        // 这两个端点只接受 GET，用 DELETE 打过去会命中 405
        var res = await _api.SendAsync(HttpMethod.Delete, path);
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, res.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(raw),
            $"405 不应是空响应体（path={path}）");

        // 405 没有专属业务码，沿用「参数不合法」这一档（与 MapStatusCode 的兜底一致）
        AssertUnifiedErrorBody(raw, Codes.InvalidArgument);
    }
}
