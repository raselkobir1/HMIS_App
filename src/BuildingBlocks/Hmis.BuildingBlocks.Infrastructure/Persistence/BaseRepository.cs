using Hmis.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Hmis.BuildingBlocks.Infrastructure.Persistence;

public abstract class BaseRepository<TEntity, TContext>(TContext context) : IRepository<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    protected TContext Context { get; } = context;
    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await Set.FindAsync([id], cancellationToken);

    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken) =>
        await Set.AddAsync(entity, cancellationToken);

    public virtual void Update(TEntity entity) => Set.Update(entity);

    public virtual void Remove(TEntity entity) => Set.Remove(entity);
}
