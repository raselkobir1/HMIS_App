using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Domain.Entities;

namespace Hmis.Modules.Identity.Application.Services;

/// <summary>Rules that protect the Super Administrator role from escalation and lock-out.</summary>
public sealed class SuperAdministratorGuard(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ICurrentUser currentUser)
{
    public async Task<Role> GetRoleAsync(CancellationToken cancellationToken) =>
        await roleRepository.GetByNormalizedNameAsync(User.Normalize(SystemRoles.SuperAdministrator), cancellationToken)
        ?? throw new InvalidOperationException("The Super Administrator role has not been seeded.");

    /// <summary>Only a Super Administrator may grant, revoke or reconfigure the Super Administrator role.</summary>
    public async Task EnsureCallerIsSuperAdministratorAsync(CancellationToken cancellationToken)
    {
        var callerId = currentUser.UserId ?? throw new UnauthorizedException();
        var roles = await userRepository.GetRoleNamesAsync(callerId, cancellationToken);

        if (!roles.Contains(SystemRoles.SuperAdministrator))
        {
            throw new BusinessException(
                IdentityErrorCodes.SuperAdministratorRequired,
                "Only a Super Administrator can change Super Administrator access.");
        }
    }

    /// <summary>Prevents removing or deactivating the last active Super Administrator.</summary>
    public async Task EnsureNotLastSuperAdministratorAsync(Guid roleId, Guid userId, CancellationToken cancellationToken)
    {
        if (await userRepository.CountActiveUsersInRoleAsync(roleId, userId, cancellationToken) == 0)
        {
            throw new BusinessException(
                IdentityErrorCodes.LastSuperAdministrator,
                "At least one active Super Administrator must remain.");
        }
    }
}
