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

[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController(IUserManager userManager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(IdentityPermissions.UsersView)]
    [ProducesResponseType<ApiResponse<PagedResponse<UserListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] UserListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await userManager.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(IdentityPermissions.UsersView)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await userManager.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(IdentityPermissions.UsersManage)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] UserCreateDto request, CancellationToken cancellationToken)
    {
        var user = await userManager.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = user.Id, version = "1" }, user, "User created successfully.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(IdentityPermissions.UsersManage)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await userManager.UpdateAsync(id, request, cancellationToken), "User updated successfully.");

    [HttpPatch("{id:guid}/status")]
    [HasPermission(IdentityPermissions.UsersManage)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UserStatusUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await userManager.SetStatusAsync(id, request, cancellationToken),
            request.IsActive ? "User activated." : "User deactivated.");

    [HttpPost("{id:guid}/reset-password")]
    [HasPermission(IdentityPermissions.UsersManage)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] UserPasswordResetDto request, CancellationToken cancellationToken)
    {
        await userManager.ResetPasswordAsync(id, request, cancellationToken);
        return OkResponse("Password reset. The user must change it at next sign-in.");
    }

    [HttpPost("{id:guid}/unlock")]
    [HasPermission(IdentityPermissions.UsersManage)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await userManager.UnlockAsync(id, cancellationToken), "User unlocked.");

    [HttpPut("{id:guid}/roles")]
    [HasPermission(IdentityPermissions.RolesManage)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] UserRolesUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await userManager.SetRolesAsync(id, request, cancellationToken), "Roles updated.");

    [HttpPut("{id:guid}/permissions")]
    [HasPermission(IdentityPermissions.RolesManage)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] UserPermissionsUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await userManager.SetPermissionsAsync(id, request, cancellationToken), "Direct permissions updated.");
}
