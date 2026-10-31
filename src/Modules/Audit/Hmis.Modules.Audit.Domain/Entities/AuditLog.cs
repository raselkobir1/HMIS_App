using Hmis.BuildingBlocks.Domain.Entities;

namespace Hmis.Modules.Audit.Domain.Entities;

/// <summary>
/// Immutable audit record. Rows are inserted by the audit writer and never updated or deleted
/// (enforced by a database trigger on audit."AuditLogs").
/// </summary>
public sealed class AuditLog : BaseEntity
{
    private AuditLog() { }

    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }
    public string? IpAddress { get; private set; }
    public string? TraceId { get; private set; }

    /// <summary>JSON (jsonb) of the values before the change.</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON (jsonb) of the values after the change.</summary>
    public string? NewValues { get; private set; }
}
