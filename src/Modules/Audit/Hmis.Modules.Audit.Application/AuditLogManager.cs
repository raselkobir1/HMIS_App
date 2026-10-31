using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Audit.Domain.Entities;

namespace Hmis.Modules.Audit.Application;

/// <summary>Read-only access to audit records. There is intentionally no add/update/delete here.</summary>
public interface IAuditLogRepository
{
    Task<PagedResponse<AuditLogListItemDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken);

    Task<AuditLogDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken);

    Task<AuditFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);
}

public interface IAuditLogManager
{
    Task<PagedResponse<AuditLogListItemDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken);

    Task<AuditLogDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AuditFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);
}

public sealed class AuditLogManager(IAuditLogRepository repository) : IAuditLogManager
{
    public Task<PagedResponse<AuditLogListItemDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public async Task<AuditLogDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(AuditLog), id);

    public Task<AuditFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken) =>
        repository.GetFilterOptionsAsync(cancellationToken);
}
