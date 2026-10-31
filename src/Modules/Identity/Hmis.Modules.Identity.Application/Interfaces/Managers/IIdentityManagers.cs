using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;

namespace Hmis.Modules.Identity.Application.Interfaces.Managers;

public interface IAuthManager
{
    Task<AuthSessionDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);

    Task<AuthSessionDto> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<AuthSessionDto> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken);

    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public interface IUserManager
{
    Task<PagedResponse<UserListItemDto>> SearchAsync(UserListQuery query, CancellationToken cancellationToken);

    Task<UserDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserDetailDto> CreateAsync(UserCreateDto request, CancellationToken cancellationToken);

    Task<UserDetailDto> UpdateAsync(Guid id, UserUpdateDto request, CancellationToken cancellationToken);

    Task<UserDetailDto> SetStatusAsync(Guid id, UserStatusUpdateDto request, CancellationToken cancellationToken);

    Task ResetPasswordAsync(Guid id, UserPasswordResetDto request, CancellationToken cancellationToken);

    Task<UserDetailDto> UnlockAsync(Guid id, CancellationToken cancellationToken);

    Task<UserDetailDto> SetRolesAsync(Guid id, UserRolesUpdateDto request, CancellationToken cancellationToken);

    Task<UserDetailDto> SetPermissionsAsync(Guid id, UserPermissionsUpdateDto request, CancellationToken cancellationToken);
}

public interface IRoleManager
{
    Task<PagedResponse<RoleListItemDto>> SearchAsync(RoleListQuery query, CancellationToken cancellationToken);

    Task<RoleDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<RoleDetailDto> CreateAsync(RoleCreateDto request, CancellationToken cancellationToken);

    Task<RoleDetailDto> UpdateAsync(Guid id, RoleUpdateDto request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<RoleDetailDto> SetPermissionsAsync(Guid id, RolePermissionsUpdateDto request, CancellationToken cancellationToken);
}

public interface IPermissionManager
{
    Task<IReadOnlyList<PermissionGroupDto>> GetGroupedAsync(CancellationToken cancellationToken);
}

public interface ILoginHistoryManager
{
    Task<PagedResponse<LoginHistoryDto>> SearchAsync(LoginHistoryQuery query, CancellationToken cancellationToken);
}
