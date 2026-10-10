using System.Net;
using Blog.Tests.Infrastructure;

namespace Blog.Tests.Integration;

/// <summary>
/// 文章与专栏的关联同步（<c>Post.CollectionLinks</c> 多对多，连接行带专栏内排序）。
///
/// <para><b>守的是同一个根因的两个症状</b>：<c>PostService.ApplyCollectionsAsync</c>
/// 完全依赖 <c>post.CollectionLinks</c> 这个导航集合来做增量同步
/// （先移除不在新列表里的、再补上缺的），而 <c>PostRepository.GetByIdAsync</c>
/// 若没有把它加载进来，集合就是空的：</para>
/// <list type="number">
///   <item>重复提交一个**已关联**的专栏 → 被当成新增 → INSERT 已存在的
///         <c>(PostId, CollectionId)</c> → 撞 <c>PK_PostCollections</c> → 500；</item>
///   <item>把 <c>collectionIds</c> 传空想取消关联 → 一个也删不掉 → 静默失败。</item>
/// </list>
/// <para>两者都是「后台打开编辑器直接保存」这种最普通的操作，
/// 所以这里既测「重复提交不炸」，也测「取消关联真的生效」。</para>
/// </summary>
[Collection(BlogApiCollection.Name)]
public sealed class PostCollectionSyncTests
{
    private readonly ApiClient _api;

    public PostCollectionSyncTests(BlogApiFixture fixture) => _api = new ApiClient(fixture.CreateClient());

    [Fact]
    public async Task 更新文章时重复提交已关联的专栏_不应撞主键()
    {
        var token = await _api.LoginAsync("admin@example.com", "Admin@12345");
        var collection = await CreateCollectionAsync(token);
        var post = await CreatePostAsync(token, [collection.Id]);

        Assert.Contains(post.Collections, c => c.Id == collection.Id);

        // 再保存一次，仍然提交同一个专栏 —— 这正是「打开编辑器直接点保存」
        var update = await UpdateAsync(token, post.Id, post.Version, [collection.Id]);

        Assert.True(update.Status == HttpStatusCode.OK,
            $"重复提交已关联的专栏应 200（修复前撞 PK_PostCollections 变 500），实际 {update.Status} / code={update.Code} / {update.Message}");
        Assert.Equal(post.Version + 1, update.Data!.Version);
        Assert.Single(update.Data.Collections);
    }

    [Fact]
    public async Task 更新文章时清空专栏_应真的取消关联()
    {
        var token = await _api.LoginAsync("admin@example.com", "Admin@12345");
        var collection = await CreateCollectionAsync(token);
        var post = await CreatePostAsync(token, [collection.Id]);

        // 传空数组 = 清空关联（传 null 才是「本次不改动」）
        var update = await UpdateAsync(token, post.Id, post.Version, []);

        Assert.True(update.Status == HttpStatusCode.OK,
            $"清空专栏关联应 200，实际 {update.Status} / code={update.Code} / {update.Message}");
        Assert.Empty(update.Data!.Collections);

        // 再读一次确认不是只在响应里「看起来」清了
        var detail = await _api.CallAsync<PostWithCollections>(HttpMethod.Get, $"/api/posts/{post.Id}");
        Assert.Empty(detail.Data!.Collections);
    }

    [Fact]
    public async Task 更新文章时更换专栏_应只剩新的那个()
    {
        var token = await _api.LoginAsync("admin@example.com", "Admin@12345");
        var first = await CreateCollectionAsync(token);
        var second = await CreateCollectionAsync(token);
        var post = await CreatePostAsync(token, [first.Id]);

        var update = await UpdateAsync(token, post.Id, post.Version, [second.Id]);

        Assert.True(update.Status == HttpStatusCode.OK,
            $"更换专栏应 200，实际 {update.Status} / code={update.Code} / {update.Message}");
        Assert.Equal([second.Id], update.Data!.Collections.Select(c => c.Id).ToArray());
    }

    // ---------------------------------------------------------------- 辅助

    private async Task<CollectionRefDto> CreateCollectionAsync(string token)
    {
        var slug = $"sync-{Guid.NewGuid():N}";

        var (status, code, data, message) = await _api.CallAsync<CollectionRefDto>(
            HttpMethod.Post, "/api/collections", token, new
            {
                title = "关联同步测试专栏",
                slug,
                description = (string?)null,
                coverImage = (string?)null,
                sortOrder = 0,
                isPublished = true,
            });

        Assert.True(status == HttpStatusCode.OK && code == Codes.Ok && data is not null,
            $"创建专栏失败 status={status} code={code} message={message}");

        return data!;
    }

    private async Task<PostWithCollections> CreatePostAsync(string token, Guid[] collectionIds)
    {
        var (status, code, data, message) = await _api.CallAsync<PostWithCollections>(
            HttpMethod.Post, "/api/posts", token, new
            {
                title = $"专栏关联-{Guid.NewGuid():N}",
                content = "正文",
                summary = (string?)null,
                coverImage = "",
                categoryId = (Guid?)null,
                tagIds = Array.Empty<Guid>(),
                collectionIds,
                authorId = (Guid?)null,
                publish = true,
            });

        Assert.True(status == HttpStatusCode.OK && code == Codes.Ok && data is not null,
            $"创建文章失败 status={status} code={code} message={message}");

        return data!;
    }

    private Task<(HttpStatusCode Status, int Code, PostWithCollections? Data, string Message)> UpdateAsync(
        string token, Guid id, int version, Guid[] collectionIds) =>
        _api.CallAsync<PostWithCollections>(HttpMethod.Put, $"/api/posts/{id}", token, new
        {
            title = "专栏关联-更新",
            content = "正文（更新）",
            summary = (string?)null,
            coverImage = "",
            categoryId = (Guid?)null,
            tagIds = Array.Empty<Guid>(),
            collectionIds,
            version,
        });
}

/// <summary>专栏响应里本文件需要用到的字段（其余字段忽略）</summary>
public sealed record CollectionRefDto(Guid Id, string Title, string Slug);

/// <summary>文章详情里本文件需要用到的字段（其余字段忽略）</summary>
public sealed record PostWithCollections(Guid Id, int Version, List<CollectionRefDto> Collections);
