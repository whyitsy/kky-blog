namespace Blog.Application.Interfaces
{
    /// <summary>
    /// 工作单元：把「提交」这件事收成一个入口，供应用服务在一次业务动作里保存多处变更。
    ///
    /// <para><b>为什么只剩 SaveChangesAsync</b></para>
    /// 早先这里还有 Begin/Commit/RollbackTransactionAsync 三个显式事务方法，
    /// 但全仓库没有任何调用方 —— 所有写操作都是「改完聚合 → 一次 SaveChanges」，
    /// 而单次 SaveChanges 本身就是 EF 的隐式事务。留着三个没人用的方法，
    /// 只会让人以为「要自己开事务」，反而多一条会写错的路。
    /// 将来真的需要跨多次 SaveChanges 的原子性时，再按当时的用例加回来。
    /// </summary>
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
