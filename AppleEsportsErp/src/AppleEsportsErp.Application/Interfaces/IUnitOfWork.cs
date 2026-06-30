namespace AppleEsportsErp.Application.Interfaces;

/// <summary>
/// Unit of Work pattern — coordinates transactions across multiple repositories.
/// SOP: All multi-table operations (login+shift, bill+payment) must be atomic.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
