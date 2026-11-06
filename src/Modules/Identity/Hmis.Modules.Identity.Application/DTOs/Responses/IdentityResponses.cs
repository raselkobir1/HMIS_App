namespace Hmis.Modules.Identity.Application.DTOs.Responses;

public sealed record CurrentUserDto(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword);

/// <summary>Returned to the client. The refresh token travels only in an HttpOnly cookie.</summary>
public sealed record AuthResponseDto(string AccessToken, DateTime AccessTokenExpiresAt, CurrentUserDto User);

/// <summary>Manager result: response body plus the refresh token the controller writes to the cookie.</summary>
public sealed record AuthSessionDto(AuthResponseDto Response, string RefreshToken, DateTime RefreshTokenExpiresAt);

public sealed record UserListItemDto(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsLockedOut,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    uint RowVersion);

public sealed record UserDetailDto(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    string? PhoneNumber,
    Guid? EmployeeId,
    bool IsActive,
    bool IsLockedOut,
    DateTime? LockoutEnd,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    IReadOnlyList<RoleReferenceDto> Roles,
    IReadOnlyList<Guid> DirectPermissionIds,
    uint RowVersion);

public sealed record RoleReferenceDto(Guid Id, string Name);

public sealed record RoleListItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    int UserCount,
    int PermissionCount);

public sealed record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<Guid> PermissionIds,
    uint RowVersion);

public sealed record PermissionDto(Guid Id, string Code, string Module, string Description, bool RequiresExplicitGrant);

public sealed record PermissionGroupDto(string Module, IReadOnlyList<PermissionDto> Permissions);

public sealed record LoginHistoryDto(
    Guid Id,
    Guid? UserId,
    string UserNameAttempted,
    bool Succeeded,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent,
    DateTime AttemptedAt);
