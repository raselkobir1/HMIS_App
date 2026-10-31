using System.Reflection;
using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hmis.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Captures changes to [Audited] entities plus queued business events, and writes them in the SAME database
/// transaction as the change (docs/ARCHITECTURE.md §8):
/// - SavingChanges: snapshot old/new values; if no transaction is active, start one.
/// - SavedChanges: write the audit rows on that connection/transaction, then commit if this interceptor started it.
/// - SaveChangesFailed: roll back if this interceptor started the transaction.
/// Properties marked [NotAudited], audit stamps and the concurrency token are excluded.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    PendingAuditEvents pendingEvents,
    AuditRecordFactory recordFactory,
    IAuditLogWriter writer) : SaveChangesInterceptor
{
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(IAuditableEntity.CreatedAt),
        nameof(IAuditableEntity.CreatedBy),
        nameof(IAuditableEntity.ModifiedAt),
        nameof(IAuditableEntity.ModifiedBy),
        nameof(IHasConcurrencyToken.RowVersion)
    ];

    private readonly Dictionary<DbContext, PendingSave> _pending = [];

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var records = CaptureChanges(context);
            records.AddRange(pendingEvents.Drain().Select(recordFactory.FromEvent));

            if (records.Count > 0)
            {
                IDbContextTransaction? ownedTransaction = null;
                if (context.Database.CurrentTransaction is null)
                {
                    ownedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
                }

                _pending[context] = new PendingSave(records, ownedTransaction);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _pending.Remove(context, out var pending))
        {
            try
            {
                var transaction = context.Database.CurrentTransaction!.GetDbTransaction();
                await writer.WriteAsync(context.Database.GetDbConnection(), transaction, pending.Records, cancellationToken);

                if (pending.OwnedTransaction is not null)
                {
                    await pending.OwnedTransaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (pending.OwnedTransaction is not null)
                {
                    await pending.OwnedTransaction.RollbackAsync(CancellationToken.None);
                }

                throw;
            }
            finally
            {
                if (pending.OwnedTransaction is not null)
                {
                    await pending.OwnedTransaction.DisposeAsync();
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _pending.Remove(context, out var pending) && pending.OwnedTransaction is not null)
        {
            await pending.OwnedTransaction.RollbackAsync(CancellationToken.None);
            await pending.OwnedTransaction.DisposeAsync();
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        eventData.Context is not null && HasAuditableWork(eventData.Context)
            ? throw new InvalidOperationException("Audited changes must be saved with SaveChangesAsync.")
            : base.SavingChanges(eventData, result);

    private bool HasAuditableWork(DbContext context) =>
        context.ChangeTracker.Entries().Any(e => IsAudited(e) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

    private List<AuditRecord> CaptureChanges(DbContext context)
    {
        var module = context is ModuleDbContext moduleContext ? moduleContext.ModuleName : context.GetType().Name;
        var records = new List<AuditRecord>();

        foreach (var entry in context.ChangeTracker.Entries().Where(IsAudited))
        {
            var entityName = entry.Metadata.ClrType.Name;
            var entityId = string.Join(",", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue));

            switch (entry.State)
            {
                case EntityState.Added:
                    records.Add(recordFactory.Create("Create", module, entityName, entityId, null, Values(entry, p => p.CurrentValue)));
                    break;

                case EntityState.Deleted:
                    records.Add(recordFactory.Create("Delete", module, entityName, entityId, Values(entry, p => p.OriginalValue), null));
                    break;

                case EntityState.Modified:
                    var changed = AuditableProperties(entry)
                        .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        .ToList();

                    // Changes limited to excluded fields (e.g. login counters) are not audited.
                    if (changed.Count > 0)
                    {
                        records.Add(recordFactory.Create("Update", module, entityName, entityId,
                            changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue),
                            changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)));
                    }

                    break;
            }
        }

        return records;
    }

    private static Dictionary<string, object?> Values(EntityEntry entry, Func<PropertyEntry, object?> selector) =>
        AuditableProperties(entry).ToDictionary(p => p.Metadata.Name, selector);

    private static IEnumerable<PropertyEntry> AuditableProperties(EntityEntry entry) =>
        entry.Properties.Where(p =>
            !p.Metadata.IsShadowProperty() &&
            !IgnoredProperties.Contains(p.Metadata.Name) &&
            p.Metadata.PropertyInfo?.GetCustomAttribute<NotAuditedAttribute>() is null);

    private static bool IsAudited(EntityEntry entry) =>
        entry.Metadata.ClrType.GetCustomAttribute<AuditedAttribute>() is not null;

    private sealed record PendingSave(List<AuditRecord> Records, IDbContextTransaction? OwnedTransaction);
}
