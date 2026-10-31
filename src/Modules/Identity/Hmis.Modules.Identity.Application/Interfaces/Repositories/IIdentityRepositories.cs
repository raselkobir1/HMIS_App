using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Models;
using Hmis.Modules.Identity.Domain.Entities;

namespace Hmis.Modules.Identity.Application.Interfaces.Repositories;

/// <summary>Unit of Work bound to the Identity module's DbContext.</summary>
public interface IIdentityUnitOfWork : IUnitOfWork;

public interface IUserRepository : IRepository<User>, IConcurrencyRepository<User>
{
    /// <summary>Loads the user with roles and direct permissions (tracked, for modification).</summary>
    Task<User?> GetWithAccessAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByNormalizedUserNameOrEmailAsync(string normalizedValue, CancellationToken cancellationToken);

    Task<bool> UserNameExistsAsync(string normalizedUserName, Guid? excludeUserId, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken);

    Task<bool> EmployeeLinkedAsync(Guid employeeId, Guid? excludeUserId, CancellationToken cancellationToken);

    Task<PagedResponse<UserListItemDto>> SearchAsync(UserListQuery query, DateTime utcNow, CancellationToken cancellationToken);

    Task<UserDetailDto?> GetDetailAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Active state, password-change flag and effective permission codes (roles ∪ direct grants).</summary>
    Task<UserAccessSnapshot?> GetAccessSnapshotAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> CountActiveUsersInRoleAsync(Guid roleId, Guid? excludeUserId, CancellationToken cancellationToken);

    Task<bool> AnyUserInRoleAsync(Guid roleId, CancellationToken cancellationToken);
}

public interface IRoleRepository : IRepository<Role>, IConcurrencyRepository<Role>
{
    Task<Role?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken);

    Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeRoleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetAllWithPermissionsAsync(CancellationToken cancellationToken);

    Task<PagedResponse<RoleListItemDto>> SearchAsync(RoleListQuery query, CancellationToken cancellationToken);

    Task<RoleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken);
}

public interface IPermissionRepository : IRepository<Permission>
{
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Permission>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, DateTime utcNow, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}

/// <summary>Append-only: no update or delete.</summary>
public interface ILoginHistoryRepository
{
    Task AddAsync(LoginHistory entry, CancellationToken cancellationToken);

    Task<PagedResponse<LoginHistoryDto>> SearchAsync(LoginHistoryQuery query, CancellationToken cancellationToken);
}
