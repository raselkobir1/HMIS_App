using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Managers;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Security;
using Hmis.Modules.Identity.Application.Services;
using Hmis.Modules.Identity.Domain.Entities;

namespace Hmis.Modules.Identity.Application.Managers;

public sealed class RoleManager(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork,
    SuperAdministratorGuard superAdminGuard,
    IAuditService auditService) : IRoleManager
{
    /// <summary>The Super Administrator role must always keep these, or the system can no longer be administered.</summary>
    private static readonly string[] SuperAdministratorRequiredPermissions =
        [IdentityPermissions.UsersManage, IdentityPermissions.RolesManage];

    public Task<PagedResponse<RoleListItemDto>> SearchAsync(RoleListQuery query, CancellationToken cancellationToken) =>
        roleRepository.SearchAsync(query, cancellationToken);

    public async Task<RoleDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await roleRepository.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Role), id);

    public async Task<RoleDetailDto> CreateAsync(RoleCreateDto request, CancellationToken cancellationToken)
    {
        await EnsureNameAvailableAsync(request.Name, null, cancellationToken);
        var permissions = await ValidatePermissionsAsync(request.PermissionIds, cancellationToken);

        var role = Role.Create(request.Name, request.Description);
        role.SetPermissions(permissions.Select(p => p.Id));
        if (permissions.Count > 0)
        {
            // Same explicit event as SetPermissionsAsync, so the audit trail shows every permission grant (from none).
            auditService.Record(new AuditEvent("PermissionsChanged", "Identity", nameof(Role), role.Id.ToString(),
                new { Permissions = Array.Empty<string>() }, new { Permissions = permissions.Select(p => p.Code).Order().ToList() }));
        }

        await roleRepository.AddAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(role.Id, cancellationToken);
    }

    public async Task<RoleDetailDto> UpdateAsync(Guid id, RoleUpdateDto request, CancellationToken cancellationToken)
    {
        var role = await GetRoleAsync(id, cancellationToken);
        roleRepository.SetExpectedRowVersion(role, request.RowVersion);
        await EnsureNameAvailableAsync(request.Name, id, cancellationToken);

        role.Update(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await GetRoleAsync(id, cancellationToken);

        if (role.IsSystem)
        {
            throw new BusinessException(IdentityErrorCodes.SystemRoleDelete, "System roles cannot be deleted.");
        }

        if (await userRepository.AnyUserInRoleAsync(id, cancellationToken))
        {
            throw new ConflictException("The role is assigned to one or more users. Remove it from those users first.",
                IdentityErrorCodes.RoleInUse);
        }

        roleRepository.Remove(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<RoleDetailDto> SetPermissionsAsync(Guid id, RolePermissionsUpdateDto request, CancellationToken cancellationToken)
    {
        var role = await GetRoleAsync(id, cancellationToken);
        var permissions = await ValidatePermissionsAsync(request.PermissionIds, cancellationToken);

        var superAdminRole = await superAdminGuard.GetRoleAsync(cancellationToken);
        if (role.Id == superAdminRole.Id)
        {
            await superAdminGuard.EnsureCallerIsSuperAdministratorAsync(cancellationToken);

            var granted = permissions.Select(p => p.Code).ToHashSet();
            if (!SuperAdministratorRequiredPermissions.All(granted.Contains))
            {
                throw new BusinessException(
                    IdentityErrorCodes.SuperAdministratorPermissionsRequired,
                    $"The Super Administrator role must keep: {string.Join(", ", SuperAdministratorRequiredPermissions)}.",
                    "permissionIds");
            }
        }

        var oldCodes = (await permissionRepository.GetByIdsAsync(role.Permissions.Select(p => p.PermissionId).ToList(), cancellationToken))
            .Select(p => p.Code).Order().ToList();
        var newCodes = permissions.Select(p => p.Code).Order().ToList();

        role.SetPermissions(permissions.Select(p => p.Id));
        auditService.Record(new AuditEvent("PermissionsChanged", "Identity", nameof(Role), role.Id.ToString(),
            new { Permissions = oldCodes }, new { Permissions = newCodes }));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<Role> GetRoleAsync(Guid id, CancellationToken cancellationToken) =>
        await roleRepository.GetWithPermissionsAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Role), id);

    private async Task EnsureNameAvailableAsync(string name, Guid? excludeRoleId, CancellationToken cancellationToken)
    {
        if (await roleRepository.NameExistsAsync(User.Normalize(name), excludeRoleId, cancellationToken))
        {
            throw new ConflictException($"A role named '{name.Trim()}' already exists.", IdentityErrorCodes.RoleNameTaken);
        }
    }

    private async Task<IReadOnlyList<Permission>> ValidatePermissionsAsync(IReadOnlyCollection<Guid> requested, CancellationToken cancellationToken)
    {
        var permissionIds = requested.Distinct().ToList();
        var found = await permissionRepository.GetByIdsAsync(permissionIds, cancellationToken);
        if (found.Count != permissionIds.Count)
        {
            throw new BusinessException(IdentityErrorCodes.PermissionNotFound, "One or more permissions do not exist.", "permissionIds");
        }

        return found;
    }
}
