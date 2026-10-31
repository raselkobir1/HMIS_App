using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Interfaces.Services;
using Hmis.Modules.Identity.Application.Options;
using Hmis.Modules.Identity.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hmis.Modules.Identity.Application.Services;

/// <summary>
/// Startup seeding (idempotent):
/// 1. Ensures the system roles exist.
/// 2. Syncs the permission catalogue declared by all modules. A permission is granted to its default roles and to
///    the Super Administrator (unless it requires an explicit grant) only when it is first created, so later
///    administrator changes are never overwritten.
/// 3. Creates the initial Super Administrator when no active one exists.
/// </summary>
public sealed class IdentitySeedTask(
    IEnumerable<IPermissionDefinitionProvider> permissionProviders,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IUserRepository userRepository,
    IIdentityUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IOptions<SeedAdminOptions> seedAdminOptions,
    ILogger<IdentitySeedTask> logger) : IStartupTask
{
    public int Order => 0;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var roles = await EnsureSystemRolesAsync(ct);
            await SyncPermissionsAsync(roles, ct);
            await EnsureSuperAdministratorAsync(roles[User.Normalize(SystemRoles.SuperAdministrator)], ct);
            return true;
        }, cancellationToken);
    }

    private async Task<Dictionary<string, Role>> EnsureSystemRolesAsync(CancellationToken cancellationToken)
    {
        var roles = (await roleRepository.GetAllWithPermissionsAsync(cancellationToken))
            .ToDictionary(r => r.NormalizedName);

        foreach (var (name, description) in SystemRoles.All)
        {
            if (roles.ContainsKey(User.Normalize(name)))
            {
                continue;
            }

            var role = Role.Create(name, description, isSystem: true);
            await roleRepository.AddAsync(role, cancellationToken);
            roles[role.NormalizedName] = role;
            logger.LogInformation("Seeded system role {Role}", name);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task SyncPermissionsAsync(Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        var definitions = permissionProviders.SelectMany(p => p.GetPermissions()).ToList();

        var duplicates = definitions.GroupBy(d => d.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"Duplicate permission codes declared: {string.Join(", ", duplicates)}");
        }

        var existing = (await permissionRepository.GetAllAsync(cancellationToken)).ToDictionary(p => p.Code);
        var superAdmin = roles[User.Normalize(SystemRoles.SuperAdministrator)];

        foreach (var definition in definitions)
        {
            if (existing.TryGetValue(definition.Code, out var permission))
            {
                permission.UpdateMetadata(definition.Module, definition.Description, definition.RequiresExplicitGrant);
                continue;
            }

            permission = Permission.Create(definition.Code, definition.Module, definition.Description, definition.RequiresExplicitGrant);
            await permissionRepository.AddAsync(permission, cancellationToken);

            if (!definition.RequiresExplicitGrant)
            {
                superAdmin.GrantPermission(permission.Id);
            }

            foreach (var roleName in definition.DefaultRoles)
            {
                if (roles.TryGetValue(User.Normalize(roleName), out var role))
                {
                    role.GrantPermission(permission.Id);
                }
                else
                {
                    logger.LogWarning("Permission {Permission} declares unknown default role {Role}", definition.Code, roleName);
                }
            }

            logger.LogInformation("Seeded permission {Permission}", definition.Code);
        }

        var declared = definitions.Select(d => d.Code).ToHashSet();
        foreach (var orphan in existing.Keys.Where(code => !declared.Contains(code)))
        {
            logger.LogWarning("Permission {Permission} exists in the database but is no longer declared by any module", orphan);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSuperAdministratorAsync(Role superAdminRole, CancellationToken cancellationToken)
    {
        if (await userRepository.CountActiveUsersInRoleAsync(superAdminRole.Id, null, cancellationToken) > 0)
        {
            return;
        }

        var options = seedAdminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning(
                "No active Super Administrator exists and Identity:SeedAdmin:Password is not configured. " +
                "Set it via user-secrets or the Identity__SeedAdmin__Password environment variable and restart.");
            return;
        }

        if (await userRepository.UserNameExistsAsync(User.Normalize(options.UserName), null, cancellationToken))
        {
            logger.LogWarning("Seed admin username {UserName} already exists but is not an active Super Administrator; skipping.", options.UserName);
            return;
        }

        var user = User.Create(options.UserName, options.FullName, options.Email, null, null, string.Empty, mustChangePassword: true);
        user.SetPassword(passwordHasher.Hash(user, options.Password), mustChangePassword: true);
        user.SetRoles([superAdminRole.Id]);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded initial Super Administrator {UserName} (password change required at first login)", options.UserName);
    }
}
