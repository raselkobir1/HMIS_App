using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Administration.Presentation.Controllers;

[Route("api/v{version:apiVersion}/departments")]
public sealed class DepartmentsController(IDepartmentManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AdministrationPermissions.DepartmentsView)]
    [ProducesResponseType<ApiResponse<PagedResponse<DepartmentDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] DepartmentListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    /// <summary>Active departments for dropdowns.</summary>
    [HttpGet("lookup")]
    [HasPermission(AdministrationPermissions.DepartmentsView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DepartmentLookupItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetLookupAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(AdministrationPermissions.DepartmentsView)]
    [ProducesResponseType<ApiResponse<DepartmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(AdministrationPermissions.DepartmentsManage)]
    [ProducesResponseType<ApiResponse<DepartmentDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] DepartmentCreateDto request, CancellationToken cancellationToken)
    {
        var department = await manager.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = department.Id, version = "1" }, department, "Department created.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AdministrationPermissions.DepartmentsManage)]
    [ProducesResponseType<ApiResponse<DepartmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DepartmentUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(id, request, cancellationToken), "Department updated.");

    [HttpPatch("{id:guid}/status")]
    [HasPermission(AdministrationPermissions.DepartmentsManage)]
    [ProducesResponseType<ApiResponse<DepartmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] StatusUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetStatusAsync(id, request, cancellationToken),
            request.IsActive ? "Department activated." : "Department deactivated.");
}

[Route("api/v{version:apiVersion}/designations")]
public sealed class DesignationsController(IDesignationManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AdministrationPermissions.DesignationsView)]
    [ProducesResponseType<ApiResponse<PagedResponse<DesignationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] DesignationListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    /// <summary>Active designations for dropdowns.</summary>
    [HttpGet("lookup")]
    [HasPermission(AdministrationPermissions.DesignationsView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DesignationLookupItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetLookupAsync(cancellationToken));

    [HttpPost]
    [HasPermission(AdministrationPermissions.DesignationsManage)]
    [ProducesResponseType<ApiResponse<DesignationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] DesignationCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CreateAsync(request, cancellationToken), "Designation created.");

    [HttpPut("{id:guid}")]
    [HasPermission(AdministrationPermissions.DesignationsManage)]
    [ProducesResponseType<ApiResponse<DesignationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DesignationUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(id, request, cancellationToken), "Designation updated.");

    [HttpPatch("{id:guid}/status")]
    [HasPermission(AdministrationPermissions.DesignationsManage)]
    [ProducesResponseType<ApiResponse<DesignationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] StatusUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetStatusAsync(id, request, cancellationToken),
            request.IsActive ? "Designation activated." : "Designation deactivated.");
}
