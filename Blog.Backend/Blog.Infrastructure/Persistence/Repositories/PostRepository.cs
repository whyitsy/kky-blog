using Blog.Domain.Entities;
using Blog.Domain.IRepository;
using Microsoft.EntityFrameworkCore;

namespace Blog.Infrastructure.Persistence.Repositories
{
    public class PostRepository : BaseRepository<Post>, IPostRepository
    {
        public PostRepository(BlogDbContext context) : base(context)
        {
        }

        /// <summary>
        /// 重写：加载文章时带上**两个多对多集合**（Tags 与 CollectionLinks，均被跟踪），
        /// 保证更新时关系是增量同步而不是全量重插。
        ///
        /// <para>⚠️ 这两个 Include 不是性能优化，是**正确性前提**：</para>
        /// <list type="bullet">
        ///   <item><c>PostService.ApplyTagsAsync</c> 与 <c>ApplyCollectionsAsync</c>
        ///   都靠「当前集合里有什么」来决定 remove / add；</item>
        ///   <item>集合没加载 → 读起来是空的 → 已存在的关联被当成新增，
        ///   INSERT 撞 <c>PK_PostCollections</c>（实测 500）；而想取消关联时又一行都删不掉
        ///   （静默失败，「保存成功但没生效」）。</item>
        /// </list>
        /// <para>同类事故在 <c>ICollectionRepository</c> 的注释里已经记录过一次 ——
        /// 结论是通用的：**要改聚合的关联集合，就得先把集合加载进来**。</para>
        /// </summary>
        public override async Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Posts
                .Include(p => p.Tags)
                .Include(p => p.CollectionLinks)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        /// <summary>
        /// 原子自增浏览量（ExecuteUpdate 不走变更追踪与乐观锁，高频访问下避免并发冲突）
        /// </summary>
        public async Task<int> IncrementViewCountAsync(Guid postId, CancellationToken cancellationToken = default)
        {
            return await _context.Posts
                .Where(p => p.Id == postId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), cancellationToken);
        }
    }
}
