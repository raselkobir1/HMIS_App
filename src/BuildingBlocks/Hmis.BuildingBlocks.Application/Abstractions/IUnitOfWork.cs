namespace Hmis.BuildingBlocks.Application.Abstractions;

/// <summary>Persistence and transaction boundary for one module's DbContext. Repositories never commit.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task BeginTransactionAsync(CancellationToken cancellationToken);

    Task CommitTransactionAsync(CancellationToken cancellationToken);

    Task RollbackTransactionAsync(CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="operation"/> in a transaction: commits on success, rolls back on exception.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
