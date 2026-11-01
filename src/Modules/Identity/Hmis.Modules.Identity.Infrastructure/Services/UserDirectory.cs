using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Identity.Infrastructure.Services;

/// <summary>Active login accounts by permission or linked employee, read from the database on each call.</summary>
public sealed class UserDirectory(IdentityDbContext context) : IUserDirectory
{
    public async Task<IReadOnlyList<Guid>> GetUserIdsWithPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        var permissionIds = context.Permissions.Where(p => p.Code == permission).Select(p => p.Id);
        var viaRoles =
            from ur in context.UserRoles
            join rp in context.RolePermissions on ur.RoleId equals rp.RoleId
            where permissionIds.Contains(rp.PermissionId)
            select ur.UserId;
        var direct = context.UserPermissions.Where(up => permissionIds.Contains(up.PermissionId)).Select(up => up.UserId);
        var ids = viaRoles.Union(direct);

        return await context.Users.AsNoTracking()
            .Where(u => u.IsActive && ids.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking().Where(u => u.IsActive && u.EmployeeId == employeeId).Select(u => u.Id).ToListAsync(cancellationToken);
}
