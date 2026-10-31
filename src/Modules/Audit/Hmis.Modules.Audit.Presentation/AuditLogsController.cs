using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Audit.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Audit.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class AuditPresentationMarker;

/// <summary>Read-only audit log. There are no write endpoints.</summary>
[Route("api/v{version:apiVersion}/audit-logs")]
[HasPermission(AuditPermissions.View)]
public sealed class AuditLogsController(IAuditLogManager auditLogManager) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResponse<AuditLogListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] AuditLogQuery query, CancellationToken cancellationToken) =>
        OkResponse(await auditLogManager.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<AuditLogDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await auditLogManager.GetByIdAsync(id, cancellationToken));

    /// <summary>Distinct modules, entity names and actions present in the log, for filter dropdowns.</summary>
    [HttpGet("filter-options")]
    [ProducesResponseType<ApiResponse<AuditFilterOptionsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilterOptions(CancellationToken cancellationToken) =>
        OkResponse(await auditLogManager.GetFilterOptionsAsync(cancellationToken));
}
