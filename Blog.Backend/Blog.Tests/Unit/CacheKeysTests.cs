using System.Text.RegularExpressions;
using Blog.Application.Interfaces;
using Blog.Application.Services.Post;

namespace Blog.Tests.Unit;

/// <summary>
/// 缓存键规范的单测。守的是「版本段只有一个来源」这条不变量：
/// 任何把 <c>v1</c> 直接写进 key 的写法，都会在版本号提升时让那部分缓存
/// **静默地不失效**（旧 key 无人访问、靠 TTL 自然消失），而这一点在任何日志里都看不出来。
/// </summary>
public sealed class CacheKeysTests
{
    /// <summary>
    /// 归档键的版本段必须与其它文章键同源。
    ///
    /// <para>这是一条**不变量守卫**：修复前 <c>PostArchives</c> 把 <c>v1</c> 硬编码在字符串里，
    /// 与 <c>CacheKeys.Version</c> 各写一份；因为当前两边恰好都是 1，它改前改后都是绿的 ——
    /// 它的价值是在**将来**有人把 Version 提到 2（或重新写死 v2）时立刻变红。</para>
    /// </summary>
    [Fact]
    public void 归档键的版本段与其它文章键一致()
    {
        var version = ExtractVersion(CacheKeys.PostList(new PostQueryRequest()));

        Assert.Contains($":v{version}:", CacheKeys.PostArchives);
    }

    /// <summary>文章相关的键都必须落在同一个前缀下 —— 否则按前缀失效会漏（归档曾是一个隐患点）</summary>
    [Fact]
    public void 文章相关键都在同一个前缀下()
    {
        Assert.StartsWith(CacheKeys.PostsPrefix, CacheKeys.PostArchives);
        Assert.StartsWith(CacheKeys.PostsPrefix, CacheKeys.PostList(new PostQueryRequest()));
        Assert.StartsWith(CacheKeys.PostsPrefix, CacheKeys.PostDetail(Guid.NewGuid()));
    }

    /// <summary>维度缺省值统一用 <c>-</c> 占位，保持 key 段数固定，便于排查</summary>
    [Fact]
    public void 归档键的固定维度用占位符()
    {
        Assert.EndsWith(":-", CacheKeys.PostArchives);
    }

    private static string ExtractVersion(string key)
    {
        var match = Regex.Match(key, ":v(\\d+):");
        Assert.True(match.Success, $"缓存键里应当有版本段：{key}");
        return match.Groups[1].Value;
    }
}
