using System.Data;
using System.Data.Common;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Infrastructure.Auditing;
using Hmis.Modules.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Audit.Infrastructure.Services;

/// <summary>
/// Inserts audit rows with plain SQL on the caller's connection and transaction, so the rows commit
/// atomically with the change being audited even though another module's DbContext owns that transaction.
/// </summary>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private const string InsertSql = $"""
        INSERT INTO {AuditDbContext.SchemaName}."{AuditDbContext.TableName}"
            ("Id", "UserId", "UserName", "Action", "Module", "EntityName", "EntityId",
             "Timestamp", "IpAddress", "TraceId", "OldValues", "NewValues")
        VALUES (@id, @userId, @userName, @action, @module, @entityName, @entityId,
                @timestamp, @ipAddress, @traceId, CAST(@oldValues AS jsonb), CAST(@newValues AS jsonb))
        """;

    public async Task WriteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlyCollection<AuditRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return;
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        foreach (var record in records)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = InsertSql;

            Add(command, "id", record.Id);
            Add(command, "userId", record.UserId);
            Add(command, "userName", Truncate(record.UserName, 100));
            Add(command, "action", record.Action);
            Add(command, "module", record.Module);
            Add(command, "entityName", record.EntityName);
            Add(command, "entityId", Truncate(record.EntityId, 200));
            Add(command, "timestamp", record.Timestamp);
            Add(command, "ipAddress", record.IpAddress);
            Add(command, "traceId", record.TraceId);
            Add(command, "oldValues", record.OldValues);
            Add(command, "newValues", record.NewValues);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? Truncate(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}

/// <summary>
/// IAuditService: Record() queues the event for the next SaveChanges in this request (atomic with the change);
/// RecordImmediateAsync() writes it now in its own transaction.
/// </summary>
public sealed class AuditService(
    PendingAuditEvents pendingEvents,
    AuditRecordFactory recordFactory,
    IAuditLogWriter writer,
    AuditDbContext auditContext) : IAuditService
{
    public void Record(AuditEvent auditEvent) => pendingEvents.Add(auditEvent);

    public async Task RecordImmediateAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var connection = auditContext.Database.GetDbConnection();
        await auditContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await writer.WriteAsync(connection, null, [recordFactory.FromEvent(auditEvent)], cancellationToken);
        }
        finally
        {
            await auditContext.Database.CloseConnectionAsync();
        }
    }
}
