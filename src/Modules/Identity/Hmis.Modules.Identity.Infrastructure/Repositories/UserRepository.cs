using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Models;
using Hmis.Modules.Identity.Domain.Entities;
using Hmis.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Identity.Infrastructure.Repositories;

public sealed class UserRepository(IdentityDbContext context)
    : BaseRepository<User, IdentityDbContext>(context), IUserRepository
{
    public Task<User?> GetWithAccessAsync(Guid id, CancellationToken cancellationToken) =>
        Set.Include(u => u.Roles)
            .Include(u => u.Permissions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByNormalizedUserNameOrEmailAsync(string normalizedValue, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedValue || u.NormalizedEmail == normalizedValue, cancellationToken);

    public Task<bool> UserNameExistsAsync(string normalizedUserName, Guid? excludeUserId, CancellationToken cancellationToken) =>
        Set.AnyAsync(u => u.NormalizedUserName == normalizedUserName && u.Id != excludeUserId, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken) =>
        Set.AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != excludeUserId, cancellationToken);

    public Task<bool> EmployeeLinkedAsync(Guid employeeId, Guid? excludeUserId, CancellationToken cancellationToken) =>
        Set.AnyAsync(u => u.EmployeeId == employeeId && u.Id != excludeUserId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken) =>
        await Set.Include(u => u.Roles).Where(u => u.EmployeeId == employeeId).ToListAsync(cancellationToken);

    public async Task<PagedResponse<UserListItemDto>> SearchAsync(UserListQuery query, DateTime utcNow, CancellationToken cancellationToken)
    {
        var users = Set.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            users = users.Where(u =>
                EF.Functions.ILike(u.UserName, pattern) ||
                EF.Functions.ILike(u.FullName, pattern) ||
                (u.Email != null && EF.Functions.ILike(u.Email, pattern)));
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(u => u.IsActive == query.IsActive.Value);
        }

        if (query.RoleId.HasValue)
        {
            users = users.Where(u => u.Roles.Any(r => r.RoleId == query.RoleId.Value));
        }

        var descending = query.SortDirection == SortDirection.Desc;
        users = query.SortBy?.ToLowerInvariant() switch
        {
            "fullname" => descending ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
            "lastloginat" => descending ? users.OrderByDescending(u => u.LastLoginAt) : users.OrderBy(u => u.LastLoginAt),
            "createdat" => descending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            _ => descending ? users.OrderByDescending(u => u.UserName) : users.OrderBy(u => u.UserName)
        };

        var total = await users.LongCountAsync(cancellationToken);
        var items = await users
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(u => new UserListItemDto(
                u.Id,
                u.UserName,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.IsActive,
                u.LockoutEnd != null && u.LockoutEnd > utcNow,
                u.MustChangePassword,
                u.LastLoginAt,
                Context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(Context.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
                    .OrderBy(name => name)
                    .ToList(),
                u.RowVersion))
            .ToListAsync(cancellationToken);

        return PagedResponse<UserListItemDto>.Create(items, total, query);
    }

    public Task<UserDetailDto?> GetDetailAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken) =>
        Set.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDetailDto(
                u.Id,
                u.UserName,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.EmployeeId,
                u.IsActive,
                u.LockoutEnd != null && u.LockoutEnd > utcNow,
                u.LockoutEnd,
                u.MustChangePassword,
                u.LastLoginAt,
                u.CreatedAt,
                Context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(Context.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r)
                    .OrderBy(r => r.Name)
                    .Select(r => new RoleReferenceDto(r.Id, r.Name))
                    .ToList(),
                u.Permissions.Select(p => p.PermissionId).ToList(),
                u.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken) =>
        await Context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(Context.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

    public async Task<UserAccessSnapshot?> GetAccessSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var state = await Set.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.MustChangePassword })
            .FirstOrDefaultAsync(cancellationToken);

        if (state is null)
        {
            return null;
        }

        var roleCodes =
            from ur in Context.UserRoles
            where ur.UserId == userId
            join rp in Context.RolePermissions on ur.RoleId equals rp.RoleId
            join p in Context.Permissions on rp.PermissionId equals p.Id
            select p.Code;

        var directCodes =
            from up in Context.UserPermissions
            where up.UserId == userId
            join p in Context.Permissions on up.PermissionId equals p.Id
            select p.Code;

        var codes = await roleCodes.Union(directCodes).ToListAsync(cancellationToken);
        return new UserAccessSnapshot(state.IsActive, state.MustChangePassword, codes);
    }

    public Task<int> CountActiveUsersInRoleAsync(Guid roleId, Guid? excludeUserId, CancellationToken cancellationToken) =>
        Set.CountAsync(u => u.IsActive && u.Id != excludeUserId && u.Roles.Any(r => r.RoleId == roleId), cancellationToken);

    public Task<bool> AnyUserInRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        Context.UserRoles.AnyAsync(ur => ur.RoleId == roleId, cancellationToken);
}
