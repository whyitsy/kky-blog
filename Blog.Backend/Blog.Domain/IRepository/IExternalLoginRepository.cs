using Blog.Domain.Entities;

namespace Blog.Domain.IRepository
{
    /// <summary>第三方身份绑定关系仓储。</summary>
    public interface IExternalLoginRepository : IBaseRepository<ExternalLogin>
    {
        /// <summary>按「平台 + 平台用户 Id」查有效绑定（软删除的不会返回）。</summary>
        Task<ExternalLogin?> GetByProviderUserAsync(
            ExternalAuthProvider provider,
            string providerUserId,
            CancellationToken cancellationToken = default);

        /// <summary>查某个本站账号在指定平台上是否有有效绑定。</summary>
        Task<ExternalLogin?> GetByUserAndProviderAsync(
            Guid userId,
            ExternalAuthProvider provider,
            CancellationToken cancellationToken = default);

        /// <summary>列出某个本站账号的全部有效绑定。</summary>
        Task<List<ExternalLogin>> GetByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
