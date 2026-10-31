using Hmis.BuildingBlocks.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Hmis.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for a module. Each module owns exactly one PostgreSQL schema, its own migrations
/// and its own migrations-history table inside that schema.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Lowercase PostgreSQL schema owned by the module (e.g. "patient").</summary>
    public abstract string Schema { get; }

    /// <summary>Module name recorded in audit logs (e.g. "Identity"). Defaults to the schema name.</summary>
    public virtual string ModuleName => Schema;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IHasConcurrencyToken).IsAssignableFrom(entityType.ClrType))
            {
                // Npgsql maps a uint row-version property to the PostgreSQL xmin system column.
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IHasConcurrencyToken.RowVersion))
                    .IsRowVersion();
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
