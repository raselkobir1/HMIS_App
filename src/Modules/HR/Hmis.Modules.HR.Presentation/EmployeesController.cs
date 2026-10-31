using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.HR.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.HR.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class HrPresentationMarker;

[Route("api/v{version:apiVersion}/employees")]
public sealed class EmployeesController(IEmployeeManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(HrPermissions.EmployeesView)]
    [ProducesResponseType<ApiResponse<PagedResponse<EmployeeListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] EmployeeListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    /// <summary>Current employees (active or on leave) for pickers.</summary>
    [HttpGet("lookup")]
    [HasPermission(HrPermissions.EmployeesView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<EmployeeLookupItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup([FromQuery] EmployeeLookupQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.LookupAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(HrPermissions.EmployeesView)]
    [ProducesResponseType<ApiResponse<EmployeeDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(HrPermissions.EmployeesManage)]
    [ProducesResponseType<ApiResponse<EmployeeDetailDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] EmployeeCreateDto request, CancellationToken cancellationToken)
    {
        var employee = await manager.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = employee.Id, version = "1" }, employee,
            $"Employee {employee.EmployeeCode} registered.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(HrPermissions.EmployeesManage)]
    [ProducesResponseType<ApiResponse<EmployeeDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] EmployeeUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(id, request, cancellationToken), "Employee updated.");

    [HttpPatch("{id:guid}/status")]
    [HasPermission(HrPermissions.EmployeesManage)]
    [ProducesResponseType<ApiResponse<EmployeeDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] EmployeeStatusUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.ChangeStatusAsync(id, request, cancellationToken), "Employment status updated.");
}
