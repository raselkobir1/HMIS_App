using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;

namespace Hmis.BuildingBlocks.Infrastructure.Auditing;

/// <summary>One fully-populated audit row, ready to be written.</summary>
public sealed record AuditRecord(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    DateTime Timestamp,
    string? IpAddress,
    string? TraceId,
    string? OldValues,
    string? NewValues);

/// <summary>
/// Persists audit records. Implemented by the Audit module, which owns the audit schema.
/// Writing on the caller's connection/transaction makes the audit row atomic with the audited change.
/// </summary>
public interface IAuditLogWriter
{
    Task WriteAsync(DbConnection connection, DbTransaction? transaction, IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken);
}

/// <summary>Request-scoped queue of business events waiting for the next SaveChanges.</summary>
public sealed class PendingAuditEvents
{
    private readonly List<AuditEvent> _events = [];

    public void Add(AuditEvent auditEvent) => _events.Add(auditEvent);

    public IReadOnlyList<AuditEvent> Drain()
    {
        var drained = _events.ToList();
        _events.Clear();
        return drained;
    }
}

/// <summary>Builds audit records with the current user, request metadata and time.</summary>
public sealed class AuditRecordFactory(ICurrentUser currentUser, IRequestContext requestContext, IDateTimeProvider dateTime)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public AuditRecord Create(string action, string module, string entityName, string entityId, object? oldValues, object? newValues) =>
        new(
            Guid.CreateVersion7(),
            currentUser.UserId,
            currentUser.UserName,
            action,
            module,
            entityName,
            entityId,
            dateTime.UtcNow,
            requestContext.IpAddress,
            string.IsNullOrEmpty(requestContext.TraceId) ? null : requestContext.TraceId,
            Serialize(oldValues),
            Serialize(newValues));

    public AuditRecord FromEvent(AuditEvent e) =>
        Create(e.Action, e.Module, e.EntityName, e.EntityId, e.OldValues, e.NewValues);

    private static string? Serialize(object? values) =>
        values is null ? null : JsonSerializer.Serialize(values, JsonOptions);
}
