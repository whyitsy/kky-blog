using Blog.Domain.Entities;
using Blog.Domain.IRepository;
using Microsoft.EntityFrameworkCore;

namespace Blog.Infrastructure.Persistence.Repositories
{
    /// <summary>第三方身份绑定仓储。查询全部走全局软删除过滤器。</summary>
    public class ExternalLoginRepository : BaseRepository<ExternalLogin>, IExternalLoginRepository
    {
        public ExternalLoginRepository(BlogDbContext context) : base(context)
        {
        }

        public Task<ExternalLogin?> GetByProviderUserAsync(
            ExternalAuthProvider provider,
            string providerUserId,
            CancellationToken cancellationToken = default)
        {
            return _context.ExternalLogins
                .FirstOrDefaultAsync(
                    x => x.Provider == provider && x.ProviderUserId == providerUserId,
                    cancellationToken);
        }

        public Task<ExternalLogin?> GetByUserAndProviderAsync(
            Guid userId,
            ExternalAuthProvider provider,
            CancellationToken cancellationToken = default)
        {
            return _context.ExternalLogins
                .FirstOrDefaultAsync(
                    x => x.UserId == userId && x.Provider == provider,
                    cancellationToken);
        }

        public Task<List<ExternalLogin>> GetByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _context.ExternalLogins
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Provider)
                .ToListAsync(cancellationToken);
        }
    }
}
