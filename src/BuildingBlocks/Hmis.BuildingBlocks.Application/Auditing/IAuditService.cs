namespace Hmis.BuildingBlocks.Application.Auditing;

/// <summary>A business event to audit (something that is not a plain entity field change).</summary>
/// <param name="Action">Verb, e.g. "RolesChanged", "PasswordReset", "View".</param>
/// <param name="Module">Owning module, e.g. "Identity".</param>
/// <param name="EntityName">Entity type, e.g. "User".</param>
/// <param name="EntityId">Identifier of the affected record.</param>
/// <param name="OldValues">Optional previous state (serialized to JSON).</param>
/// <param name="NewValues">Optional new state (serialized to JSON).</param>
public sealed record AuditEvent(
    string Action,
    string Module,
    string EntityName,
    string EntityId,
    object? OldValues = null,
    object? NewValues = null);

/// <summary>
/// Explicit audit recording. Entity changes on [Audited] entities are captured automatically;
/// use this for business events.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Queues the event; it is written in the same database transaction as the next SaveChanges of this request,
    /// so it commits or rolls back together with the change it describes.
    /// </summary>
    void Record(AuditEvent auditEvent);

    /// <summary>Writes the event immediately in its own transaction (for events with no data change, e.g. sensitive reads).</summary>
    Task RecordImmediateAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
