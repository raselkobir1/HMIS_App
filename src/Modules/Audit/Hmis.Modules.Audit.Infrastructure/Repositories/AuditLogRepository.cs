using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Audit.Application;
using Hmis.Modules.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Audit.Infrastructure.Repositories;

public sealed class AuditLogRepository(AuditDbContext context) : IAuditLogRepository
{
    public async Task<PagedResponse<AuditLogListItemDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = context.AuditLogs.AsNoTracking();

        if (query.FromUtc.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Utc);
            logs = logs.Where(l => l.Timestamp >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Utc);
            logs = logs.Where(l => l.Timestamp <= to);
        }

        if (query.UserId.HasValue)
        {
            logs = logs.Where(l => l.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            logs = logs.Where(l => l.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            logs = logs.Where(l => l.EntityName == query.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            logs = logs.Where(l => l.EntityId == query.EntityId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(l => l.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            logs = logs.Where(l => l.UserName != null && EF.Functions.ILike(l.UserName, pattern));
        }

        var total = await logs.LongCountAsync(cancellationToken);
        var items = await logs
            .OrderByDescending(l => l.Timestamp)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(l => new AuditLogListItemDto(
                l.Id, l.Timestamp, l.UserId, l.UserName, l.Action, l.Module, l.EntityName, l.EntityId, l.IpAddress))
            .ToListAsync(cancellationToken);

        return PagedResponse<AuditLogListItemDto>.Create(items, total, query);
    }

    public Task<AuditLogDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken) =>
        context.AuditLogs.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new AuditLogDetailDto(
                l.Id, l.Timestamp, l.UserId, l.UserName, l.Action, l.Module, l.EntityName, l.EntityId,
                l.IpAddress, l.TraceId, l.OldValues, l.NewValues))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AuditFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var modules = await context.AuditLogs.Select(l => l.Module).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var entities = await context.AuditLogs.Select(l => l.EntityName).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var actions = await context.AuditLogs.Select(l => l.Action).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        return new AuditFilterOptionsDto(modules, entities, actions);
    }
}
