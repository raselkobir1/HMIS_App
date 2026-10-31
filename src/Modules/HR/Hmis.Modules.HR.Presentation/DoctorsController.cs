using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.HR.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.HR.Presentation;

[Route("api/v{version:apiVersion}/doctors")]
public sealed class DoctorsController(IDoctorManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(DoctorPermissions.View)]
    [ProducesResponseType<ApiResponse<PagedResponse<DoctorDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] DoctorListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(DoctorPermissions.View)]
    [ProducesResponseType<ApiResponse<DoctorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(DoctorPermissions.Manage)]
    [ProducesResponseType<ApiResponse<DoctorDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] DoctorCreateDto request, CancellationToken cancellationToken)
    {
        var doctor = await manager.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = doctor.Id, version = "1" }, doctor, "Doctor profile created.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(DoctorPermissions.Manage)]
    [ProducesResponseType<ApiResponse<DoctorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DoctorUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(id, request, cancellationToken), "Doctor profile updated.");
}
