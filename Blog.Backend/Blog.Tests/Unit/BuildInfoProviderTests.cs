using Blog.Application.Interfaces;
using Blog.Infrastructure.Diagnostics;

namespace Blog.Tests.Unit;

/// <summary>
/// <see cref="BuildInfoProvider"/> 的三层兜底测试。
///
/// <para><b>为什么这组测试重要</b></para>
/// 这个端点唯一的用途就是「线上排障时确认版本」，而线上恰恰是**只有环境变量那一层**
/// 能生效的地方（容器里由 Dockerfile 的 ENV 提供，见 deploy/webapi.Dockerfile）。
/// 如果这层读错了，表现是版本号显示成 <c>0.0.0-local</c> —— 一个看起来"有值"
/// 但完全误导人的结果，比报错更难发现。
/// </summary>
public sealed class BuildInfoProviderTests
{
    [Fact]
    public void 环境变量齐全时_原样返回()
    {
        var provider = new BuildInfoProvider(new BuildInfoOptions
        {
            Version = "v2026.09.17",
            Commit = "0be5460abcdef1234567890abcdef1234567890",
            BuiltAt = "2026-09-17T10:00:00Z",
        });

        var info = provider.Get();

        Assert.Equal("v2026.09.17", info.Version);
        Assert.Equal("0be5460abcdef1234567890abcdef1234567890", info.Commit);
        Assert.Equal("2026-09-17T10:00:00Z", info.BuiltAt);
    }

    /// <summary>本地开发：没有注入任何变量，版本号应是可读的兜底值，而不是空串或抛异常</summary>
    [Fact]
    public void 未注入版本时_返回可读的兜底值()
    {
        var provider = new BuildInfoProvider(new BuildInfoOptions());

        var info = provider.Get();

        Assert.Equal(BuildInfoProvider.UnknownVersion, info.Version);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
    }

    /// <summary>只填了部分环境变量时，其余字段仍要合法（CI 注入不全也不该让端点坏掉）</summary>
    [Fact]
    public void 只注入版本时_其余字段仍有兜底()
    {
        var provider = new BuildInfoProvider(new BuildInfoOptions { Version = "v2026.09.17" });

        var info = provider.Get();

        Assert.Equal("v2026.09.17", info.Version);
        Assert.Equal(string.Empty, info.Commit); // 明确为空，而不是 null
        Assert.False(string.IsNullOrWhiteSpace(info.BuiltAt));
    }

    /// <summary>环境变量值为纯空白时按「未注入」处理，不能原样返回空白字符串</summary>
    [Fact]
    public void 注入空白值_按未注入处理()
    {
        var provider = new BuildInfoProvider(new BuildInfoOptions
        {
            Version = "   ",
            Commit = "  ",
            BuiltAt = "\t",
        });

        var info = provider.Get();

        Assert.Equal(BuildInfoProvider.UnknownVersion, info.Version);
        Assert.Equal(string.Empty, info.Commit);
        Assert.False(string.IsNullOrWhiteSpace(info.BuiltAt));
    }

    /// <summary>注入值两端的空白要去掉（CI 传参偶尔会带上来）</summary>
    [Fact]
    public void 注入值两端有空白_应去掉()
    {
        var provider = new BuildInfoProvider(new BuildInfoOptions
        {
            Version = " v2026.09.17 ",
            Commit = " abc123 ",
        });

        var info = provider.Get();

        Assert.Equal("v2026.09.17", info.Version);
        Assert.Equal("abc123", info.Commit);
    }

    /// <summary>commit 允许为空（本地构建没有 SHA），但不能是 "unknown" 这类假值</summary>
    [Fact]
    public void 未注入commit时_为空串而不是假值()
    {
        var provider = new BuildInfoProvider(new BuildInfoOptions());

        var info = provider.Get();

        Assert.Equal(string.Empty, info.Commit);
        Assert.NotEqual(BuildInfoProvider.UnknownVersion, info.Commit);
    }

    /// <summary>
    /// 构建时间缺失时用「当前时刻」而不是空串：
    /// 空串会让调用方以为字段解析失败，而"这个进程什么时候起来的"同样有排障价值。
    /// </summary>
    [Fact]
    public void 未注入构建时间时_回退到当前时间()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var provider = new BuildInfoProvider(new BuildInfoOptions());

        var info = provider.Get();

        Assert.True(DateTimeOffset.TryParse(info.BuiltAt, out var parsed), $"builtAt 应是可解析的时间：{info.BuiltAt}");
        Assert.InRange(parsed, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }
}
