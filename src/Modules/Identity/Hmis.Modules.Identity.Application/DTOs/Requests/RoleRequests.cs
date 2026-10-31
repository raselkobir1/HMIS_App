using Hmis.BuildingBlocks.Application.Pagination;

namespace Hmis.Modules.Identity.Application.DTOs.Requests;

public sealed class RoleListQuery : PaginationRequest;

public sealed record RoleCreateDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<Guid> PermissionIds { get; init; } = [];
}

public sealed record RoleUpdateDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record RolePermissionsUpdateDto
{
    public IReadOnlyList<Guid> PermissionIds { get; init; } = [];
}

public sealed class LoginHistoryQuery : PaginationRequest
{
    public Guid? UserId { get; set; }
    public bool? Succeeded { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
