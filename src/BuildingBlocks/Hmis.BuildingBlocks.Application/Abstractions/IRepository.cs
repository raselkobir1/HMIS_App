namespace Hmis.BuildingBlocks.Application.Abstractions;

/// <summary>Common persistence operations. Module repositories extend this with module-specific queries.</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(T entity, CancellationToken cancellationToken);

    void Update(T entity);

    void Remove(T entity);
}
