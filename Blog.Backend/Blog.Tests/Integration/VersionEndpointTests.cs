using System.Net;
using System.Text.Json;
using Blog.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Blog.Tests.Integration;

/// <summary>只声明测试用到的字段，避免与后端 DTO 强耦合</summary>
public sealed record VersionDto(string Version, string Commit, string BuiltAt);

/// <summary>
/// <c>GET /api/version</c> 契约测试。
///
/// <para><b>这个端点是干什么的</b></para>
/// 「线上跑的是哪一版」必须能**从外部一句话问出来**。在此之前只能靠
/// <c>docker inspect</c> 反推镜像 tag，或者 SSH 上服务器翻 compose 文件 ——
/// 排障时多绕好几步，而且容易看错（`:latest` 是会飘的）。
///
/// <para><b>为什么要专门测它</b></para>
/// 它的价值全在"能在生产里安全地暴露"。因此两条硬约束：
///   1. **匿名可访问**（运维/负载均衡要能问，不该为了它配 token）
///   2. **绝不泄露内部细节**（返回版本/SHA/构建时间即可，不能带连接串、路径、环境变量）
/// 第 2 条尤其容易在"顺手多加点调试信息"时被破坏，所以用测试钉住字段集合。
/// </summary>
[Collection(BlogApiCollection.Name)]
public sealed class VersionEndpointTests
{
    private readonly BlogApiFixture _fixture;
    private readonly ApiClient _api;

    public VersionEndpointTests(BlogApiFixture fixture)
    {
        _fixture = fixture;
        _api = new ApiClient(fixture.CreateClient());
    }

    /// <summary>
    /// **最关键的一条**：模拟 CI/容器注入环境变量后，端点必须原样返回那三个值。
    ///
    /// 为什么要专门测这条：单元测试只覆盖了「给 BuildInfoProvider 一个 options，它返回什么」，
    /// 而真正容易断的是**配置的接线** —— 环境变量名写错、DI 里读错了 key、
    /// 或者应用层配置源被覆盖。这类错误在单元测试里完全看不见，
    /// 表现却是线上 /api/version 一直显示 "unknown"（看起来"有值"，最难发现）。
    ///
    /// 这里用 WithWebHostBuilder 追加一层在**工厂配置之后**的内存配置源，
    /// 复现的正是 EnvironmentVariablesConfigurationProvider 会提供的 key。
    /// </summary>
    [Fact]
    public async Task 版本端点_注入环境变量后_原样返回版本与commit()
    {
        using var custom = _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["VERSION"] = "v0.1.0",
                    ["GIT_SHA"] = "0be5460abcdef1234567890abcdef1234567890",
                    ["BUILD_TIME"] = "2026-09-17T10:00:00Z",
                }));
        });

        var client = new ApiClient(custom.CreateClient());
        var (status, code, data, message) = await client.CallAsync<VersionDto>(
            HttpMethod.Get, "/api/version");

        Assert.True(status == HttpStatusCode.OK && code == Codes.Ok,
            $"应返回 200/0，实际 {status}/{code} {message}");

        Assert.NotNull(data);
        Assert.Equal("v0.1.0", data!.Version);
        Assert.Equal("0be5460abcdef1234567890abcdef1234567890", data.Commit);
        Assert.Equal("2026-09-17T10:00:00Z", data.BuiltAt);
    }

    /// <summary>版本端点必须匿名可访问 —— 排障时不该为了问一句版本去配 token</summary>
    [Fact]
    public async Task 版本端点_不需要登录()
    {
        // 不带任何 Authorization 头，且明确不跟随任何客户端凭据
        var res = await _api.SendAsync(HttpMethod.Get, "/api/version");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task 版本端点_匿名可访问_返回统一响应体()
    {
        var res = await _api.SendAsync(HttpMethod.Get, "/api/version");
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        Assert.Equal(Codes.Ok, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("version", out var version), $"缺少 version：{raw}");
        Assert.False(string.IsNullOrWhiteSpace(version.GetString()), "version 不能为空");

        // commit 允许为空（本地开发不在 CI 里构建，拿不到 SHA），但字段必须在 —— 契约稳定
        Assert.True(data.TryGetProperty("commit", out _), $"缺少 commit：{raw}");
        Assert.True(data.TryGetProperty("builtAt", out _), $"缺少 builtAt：{raw}");
    }

    /// <summary>
    /// 反面断言：只暴露这三个字段。
    /// 「顺手加点环境信息」是这个端点最常见的安全退化方向 ——
    /// 它匿名可访问，多一个字段就等于多一次信息泄露。
    /// </summary>
    [Fact]
    public async Task 版本端点_只暴露三个字段_不泄露内部细节()
    {
        var res = await _api.SendAsync(HttpMethod.Get, "/api/version");
        var raw = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var data = doc.RootElement.GetProperty("data");

        var fields = data.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "builtAt", "commit", "version" }, fields);

        // 值里不能夹带路径 / 连接串之类的痕迹
        foreach (var property in data.EnumerateObject())
        {
            var value = property.Value.GetString() ?? string.Empty;
            Assert.DoesNotContain("/app/", value);
            Assert.DoesNotContain("ConnectionString", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>开发/测试环境下没有注入版本信息时，也要给出可读的占位值而不是空串或抛异常</summary>
    [Fact]
    public async Task 版本端点_未注入构建信息时有兜底值()
    {
        var res = await _api.SendAsync(HttpMethod.Get, "/api/version");
        var raw = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var data = doc.RootElement.GetProperty("data");

        var version = data.GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(version));

        var builtAt = data.GetProperty("builtAt").GetString();
        Assert.False(string.IsNullOrWhiteSpace(builtAt));
    }
}
