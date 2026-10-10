using Blog.Domain.Entities.Base;

namespace Blog.Domain.Entities;


public class Post : BaseEntity
{
    private const int SummaryLength = 50;

    public DateTimeOffset? UpdatedAt { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;

    /// <summary>摘要。为空时自动取正文前 50 字；作者填写后不再被自动覆盖</summary>
    public string Summary { get; private set; } = string.Empty;

    public string CoverImage { get; private set; } = string.Empty;

    /// <summary>
    /// 署名作者。可空。⚠️ Author 是**软删除**（UPDATE IsDeleted，不是 DELETE），
    /// 因此数据库层的 ON DELETE SET NULL 不会触发，本列仍指向那条已软删的作者；
    /// 表现为文章详情里的作者名变 null（全局查询过滤器把已软删的作者挡掉了）。
    /// </summary>
    public Guid? AuthorId { get; private set; }

    /// <summary>
    /// 创建者账号。用于归属校验（谁能改这篇文章）与审计。
    /// 与 AuthorId 同理：账号软删除后本列**不会**被置空（FK 的 SET NULL 只在真 DELETE 时生效）。
    /// </summary>
    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }
    public bool IsPublished => PublishedAt.HasValue;
    public int ViewCount { get; private set; }

    /// <summary>文章字数（字符数），写入时计算，用于站点统计</summary>
    public int WordCount { get; private set; }

    public Guid? CategoryId { get; set; } // 可空：配置的删除关系为 SetNull

    // 导航属性
    public Category? Category { get; set; }
    public ICollection<Tag> Tags { get; set; } = [];
    public ICollection<PostCollection> CollectionLinks { get; set; } = [];
    public Author? Author { get; set; }

    private Post() { } // EF Core 需要一个无参构造函数

    public Post(string title, string content, string? summary, Guid? authorId, Guid? categoryId,
        string coverImage, Guid? createdByUserId = null, bool publishNow = false)
    {
        Title = title;
        Content = content;
        AuthorId = authorId;
        CategoryId = categoryId;
        CoverImage = coverImage;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTimeOffset.UtcNow;

        // 摘要：作者填了就用作者的，没填才自动截取
        Summary = string.IsNullOrWhiteSpace(summary) ? BuildAutoSummary(content) : summary.Trim();
        WordCount = content?.Length ?? 0;

        if (publishNow) PublishedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 更新文章。
    /// 摘要规则（由应用层判定，不新增字段）：<paramref name="summary"/> 为空则重新自动截取，
    /// 非空则使用传入值 —— 因此作者显式填写的摘要不会被后续更新覆盖。
    /// </summary>
    public void Update(string title, string content, string? summary, Guid? categoryId, string coverImage)
    {
        Title = title;
        Content = content;
        CategoryId = categoryId;
        CoverImage = coverImage;
        UpdatedAt = DateTimeOffset.UtcNow;

        Summary = string.IsNullOrWhiteSpace(summary) ? BuildAutoSummary(content) : summary.Trim();
        WordCount = content?.Length ?? 0;
    }

    private static string BuildAutoSummary(string? content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return content.Length <= SummaryLength ? content : content[..SummaryLength];
    }

    public void Publish()
    {
        // 幂等处理：重复发布直接保持原发布时间，避免覆盖首次发布时间（影响归档排序）
        PublishedAt ??= DateTimeOffset.UtcNow;
    }

    public void Unpublish()
    {
        PublishedAt = null;
    }

}
