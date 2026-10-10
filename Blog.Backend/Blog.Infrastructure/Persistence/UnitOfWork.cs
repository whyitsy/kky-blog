using Blog.Application.Interfaces;

namespace Blog.Infrastructure.Persistence
{
    /// <summary>
    /// 工作单元的最薄实现：直接把 SaveChangesAsync 转给 DbContext。
    ///
    /// <para>BlogDbContext 的生命周期由 DI 容器管理（Scoped），本类不持有任何需要释放的资源 ——
    /// 因此它不实现 IDisposable（原先实现只是为了释放显式事务，而事务方法已随死代码一并删除）。</para>
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly BlogDbContext _dbContext;

        public UnitOfWork(BlogDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
