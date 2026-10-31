using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Managers;
using Hmis.Modules.Identity.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Identity.Presentation.Controllers;

[Route("api/v{version:apiVersion}/roles")]
public sealed class RolesController(IRoleManager roleManager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(IdentityPermissions.RolesView)]
    [ProducesResponseType<ApiResponse<PagedResponse<RoleListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] RoleListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await roleManager.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(IdentityPermissions.RolesView)]
    [ProducesResponseType<ApiResponse<RoleDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await roleManager.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(IdentityPermissions.RolesManage)]
    [ProducesResponseType<ApiResponse<RoleDetailDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] RoleCreateDto request, CancellationToken cancellationToken)
    {
        var role = await roleManager.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = role.Id, version = "1" }, role, "Role created successfully.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(IdentityPermissions.RolesManage)]
    [ProducesResponseType<ApiResponse<RoleDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoleUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await roleManager.UpdateAsync(id, request, cancellationToken), "Role updated successfully.");

    [HttpDelete("{id:guid}")]
    [HasPermission(IdentityPermissions.RolesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await roleManager.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/permissions")]
    [HasPermission(IdentityPermissions.RolesManage)]
    [ProducesResponseType<ApiResponse<RoleDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] RolePermissionsUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await roleManager.SetPermissionsAsync(id, request, cancellationToken), "Role permissions updated.");
}

[Route("api/v{version:apiVersion}/permissions")]
public sealed class PermissionsController(IPermissionManager permissionManager) : ApiControllerBase
{
    /// <summary>The permission catalogue, grouped by module.</summary>
    [HttpGet]
    [HasPermission(IdentityPermissions.RolesView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PermissionGroupDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        OkResponse(await permissionManager.GetGroupedAsync(cancellationToken));
}

[Route("api/v{version:apiVersion}/login-history")]
public sealed class LoginHistoryController(ILoginHistoryManager loginHistoryManager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(IdentityPermissions.LoginHistoryView)]
    [ProducesResponseType<ApiResponse<PagedResponse<LoginHistoryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] LoginHistoryQuery query, CancellationToken cancellationToken) =>
        OkResponse(await loginHistoryManager.SearchAsync(query, cancellationToken));
}
