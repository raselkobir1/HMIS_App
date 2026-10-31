using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Managers;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Interfaces.Services;
using Hmis.Modules.Identity.Application.Services;
using Hmis.Modules.Identity.Domain.Entities;

namespace Hmis.Modules.Identity.Application.Managers;

public sealed class UserManager(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IIdentityUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    SuperAdministratorGuard superAdminGuard,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    IAuditService auditService,
    IEmployeeLookup employeeLookup) : IUserManager
{
    private const string Module = "Identity";

    public Task<PagedResponse<UserListItemDto>> SearchAsync(UserListQuery query, CancellationToken cancellationToken) =>
        userRepository.SearchAsync(query, dateTime.UtcNow, cancellationToken);

    public async Task<UserDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await userRepository.GetDetailAsync(id, dateTime.UtcNow, cancellationToken)
        ?? throw new NotFoundException(nameof(User), id);

    public async Task<UserDetailDto> CreateAsync(UserCreateDto request, CancellationToken cancellationToken)
    {
        await EnsureUserNameAvailableAsync(request.UserName, cancellationToken);
        await EnsureEmailAvailableAsync(request.Email, null, cancellationToken);
        await EnsureEmployeeLinkableAsync(request.EmployeeId, null, cancellationToken);
        var roles = await ValidateRoleChangeAsync([], request.RoleIds, cancellationToken);

        var user = User.Create(request.UserName, request.FullName, request.Email, request.PhoneNumber, request.EmployeeId,
            passwordHash: string.Empty, mustChangePassword: true);
        user.SetPassword(passwordHasher.Hash(user, request.Password), mustChangePassword: true);
        user.SetRoles(roles.Select(r => r.Id));
        auditService.Record(new AuditEvent("RolesChanged", Module, nameof(User), user.Id.ToString(),
            null, new { Roles = roles.Select(r => r.Name).Order().ToList() }));

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserDetailDto> UpdateAsync(Guid id, UserUpdateDto request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(id, cancellationToken);
        userRepository.SetExpectedRowVersion(user, request.RowVersion);
        await EnsureEmailAvailableAsync(request.Email, id, cancellationToken);
        if (request.EmployeeId != user.EmployeeId)
        {
            await EnsureEmployeeLinkableAsync(request.EmployeeId, id, cancellationToken);
        }

        user.UpdateProfile(request.FullName, request.Email, request.PhoneNumber, request.EmployeeId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserDetailDto> SetStatusAsync(Guid id, UserStatusUpdateDto request, CancellationToken cancellationToken)
    {
        EnsureNotSelf(id, "You cannot change the status of your own account.");
        var user = await GetUserAsync(id, cancellationToken);

        if (request.IsActive)
        {
            user.Activate();
        }
        else if (user.IsActive)
        {
            var superAdminRole = await superAdminGuard.GetRoleAsync(cancellationToken);
            if (user.HasRole(superAdminRole.Id))
            {
                await superAdminGuard.EnsureCallerIsSuperAdministratorAsync(cancellationToken);
                await superAdminGuard.EnsureNotLastSuperAdministratorAsync(superAdminRole.Id, user.Id, cancellationToken);
            }

            user.Deactivate();
            await RevokeSessionsAsync(user.Id, "User deactivated", cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, UserPasswordResetDto request, CancellationToken cancellationToken)
    {
        EnsureNotSelf(id, "Use Change Password to change your own password.");
        var user = await GetUserAsync(id, cancellationToken);
        await EnsureCanManageSuperAdministratorAsync(user, cancellationToken);

        user.SetPassword(passwordHasher.Hash(user, request.NewPassword), mustChangePassword: true);
        user.Unlock();
        auditService.Record(new AuditEvent("PasswordReset", Module, nameof(User), user.Id.ToString()));
        await RevokeSessionsAsync(user.Id, "Password reset by administrator", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDetailDto> UnlockAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(id, cancellationToken);
        user.Unlock();
        auditService.Record(new AuditEvent("Unlocked", Module, nameof(User), user.Id.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserDetailDto> SetRolesAsync(Guid id, UserRolesUpdateDto request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(id, cancellationToken);
        var currentRoleIds = user.Roles.Select(r => r.RoleId).ToList();
        var oldRoleNames = await userRepository.GetRoleNamesAsync(user.Id, cancellationToken);
        var roles = await ValidateRoleChangeAsync(currentRoleIds, request.RoleIds, cancellationToken, user);

        user.SetRoles(roles.Select(r => r.Id));
        auditService.Record(new AuditEvent("RolesChanged", Module, nameof(User), user.Id.ToString(),
            new { Roles = oldRoleNames }, new { Roles = roles.Select(r => r.Name).Order().ToList() }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserDetailDto> SetPermissionsAsync(Guid id, UserPermissionsUpdateDto request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(id, cancellationToken);
        var permissionIds = request.PermissionIds.Distinct().ToList();

        var found = await permissionRepository.GetByIdsAsync(permissionIds, cancellationToken);
        if (found.Count != permissionIds.Count)
        {
            throw new BusinessException(IdentityErrorCodes.PermissionNotFound, "One or more permissions do not exist.", "permissionIds");
        }

        var oldIds = user.Permissions.Select(p => p.PermissionId).ToList();
        var oldCodes = (await permissionRepository.GetByIdsAsync(oldIds, cancellationToken)).Select(p => p.Code).Order().ToList();

        user.SetDirectPermissions(permissionIds);
        auditService.Record(new AuditEvent("PermissionsChanged", Module, nameof(User), user.Id.ToString(),
            new { Permissions = oldCodes }, new { Permissions = found.Select(p => p.Code).Order().ToList() }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<User> GetUserAsync(Guid id, CancellationToken cancellationToken) =>
        await userRepository.GetWithAccessAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(User), id);

    /// <summary>Checks the requested roles exist and applies the Super Administrator escalation/lock-out rules.</summary>
    private async Task<IReadOnlyList<Role>> ValidateRoleChangeAsync(
        IReadOnlyCollection<Guid> currentRoleIds,
        IReadOnlyCollection<Guid> requestedRoleIds,
        CancellationToken cancellationToken,
        User? user = null)
    {
        var roleIds = requestedRoleIds.Distinct().ToList();
        var roles = await roleRepository.GetByIdsAsync(roleIds, cancellationToken);
        if (roles.Count != roleIds.Count)
        {
            throw new BusinessException(IdentityErrorCodes.RoleNotFound, "One or more roles do not exist.", "roleIds");
        }

        var superAdminRole = await superAdminGuard.GetRoleAsync(cancellationToken);
        var hadSuperAdmin = currentRoleIds.Contains(superAdminRole.Id);
        var willHaveSuperAdmin = roleIds.Contains(superAdminRole.Id);

        if (hadSuperAdmin != willHaveSuperAdmin)
        {
            await superAdminGuard.EnsureCallerIsSuperAdministratorAsync(cancellationToken);
        }

        if (user is not null && hadSuperAdmin && !willHaveSuperAdmin)
        {
            EnsureNotSelf(user.Id, "You cannot remove the Super Administrator role from your own account.");
            if (user.IsActive)
            {
                await superAdminGuard.EnsureNotLastSuperAdministratorAsync(superAdminRole.Id, user.Id, cancellationToken);
            }
        }

        return roles;
    }

    private async Task EnsureCanManageSuperAdministratorAsync(User user, CancellationToken cancellationToken)
    {
        var superAdminRole = await superAdminGuard.GetRoleAsync(cancellationToken);
        if (user.HasRole(superAdminRole.Id))
        {
            await superAdminGuard.EnsureCallerIsSuperAdministratorAsync(cancellationToken);
        }
    }

    private async Task EnsureUserNameAvailableAsync(string userName, CancellationToken cancellationToken)
    {
        if (await userRepository.UserNameExistsAsync(User.Normalize(userName), null, cancellationToken))
        {
            throw new ConflictException($"Username '{userName}' is already in use.", IdentityErrorCodes.UserNameTaken);
        }
    }

    private async Task EnsureEmailAvailableAsync(string? email, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(email) &&
            await userRepository.EmailExistsAsync(User.Normalize(email), excludeUserId, cancellationToken))
        {
            throw new ConflictException($"Email '{email}' is already in use.", IdentityErrorCodes.EmailTaken);
        }
    }

    /// <summary>A login can be linked only to a current employee who has no other login account.</summary>
    private async Task EnsureEmployeeLinkableAsync(Guid? employeeId, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        if (employeeId is null)
        {
            return;
        }

        if (await employeeLookup.GetAsync(employeeId.Value, cancellationToken) is not { IsCurrent: true })
        {
            throw new BusinessException(IdentityErrorCodes.EmployeeInvalid, "Select a current employee.", "employeeId");
        }

        if (await userRepository.EmployeeLinkedAsync(employeeId.Value, excludeUserId, cancellationToken))
        {
            throw new ConflictException("This employee is already linked to another login account.", IdentityErrorCodes.EmployeeAlreadyLinked);
        }
    }

    private void EnsureNotSelf(Guid userId, string message)
    {
        if (currentUser.UserId == userId)
        {
            throw new BusinessException(IdentityErrorCodes.CannotChangeOwnAccount, message);
        }
    }

    private async Task RevokeSessionsAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var now = dateTime.UtcNow;
        foreach (var token in await refreshTokenRepository.GetActiveByUserAsync(userId, now, cancellationToken))
        {
            token.Revoke(now, reason);
        }
    }
}
