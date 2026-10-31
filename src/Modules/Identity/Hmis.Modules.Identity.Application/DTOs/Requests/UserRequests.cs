using Hmis.BuildingBlocks.Application.Pagination;

namespace Hmis.Modules.Identity.Application.DTOs.Requests;

public sealed class UserListQuery : PaginationRequest
{
    public bool? IsActive { get; set; }
    public Guid? RoleId { get; set; }
}

public sealed record UserCreateDto
{
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public Guid? EmployeeId { get; init; }

    /// <summary>Temporary password; the user must change it at first login.</summary>
    public string Password { get; init; } = string.Empty;

    public IReadOnlyList<Guid> RoleIds { get; init; } = [];
}

public sealed record UserUpdateDto
{
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public Guid? EmployeeId { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record UserStatusUpdateDto
{
    public bool IsActive { get; init; }
}

public sealed record UserPasswordResetDto
{
    /// <summary>New temporary password; the user must change it at next login.</summary>
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record UserRolesUpdateDto
{
    public IReadOnlyList<Guid> RoleIds { get; init; } = [];
}

public sealed record UserPermissionsUpdateDto
{
    public IReadOnlyList<Guid> PermissionIds { get; init; } = [];
}
