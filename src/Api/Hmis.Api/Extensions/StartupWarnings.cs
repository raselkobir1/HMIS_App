using Hmis.BuildingBlocks.Application.Security;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Options;
using Hmis.Modules.Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Hmis.Api.Extensions;

/// <summary>
/// Post-startup checks for conditions that leave a fresh install unusable but do not stop the host.
/// They only log; the business rules themselves live in the modules.
/// </summary>
public static class StartupWarnings
{
    /// <summary>
    /// Runs after the startup tasks (seeding). When no active Super Administrator exists afterwards, nobody can sign
    /// in to administer the system; the Identity seed logs a warning for this, which is easy to miss in a log stream,
    /// so it is repeated here at error level with the exact remedy.
    /// </summary>
    public static async Task LogStartupWarningsAsync(this WebApplication app)
    {
        var logger = app.Logger;
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var seedAdmin = scope.ServiceProvider.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
            var cancellationToken = app.Lifetime.ApplicationStopping;

            var superAdminRole = await roles.GetByNormalizedNameAsync(User.Normalize(SystemRoles.SuperAdministrator), cancellationToken);
            if (superAdminRole is null)
            {
                logger.LogError("STARTUP CHECK FAILED: the '{Role}' role does not exist, so the system cannot be administered. The Identity seed did not run.", SystemRoles.SuperAdministrator);
                return;
            }

            if (await users.CountActiveUsersInRoleAsync(superAdminRole.Id, null, cancellationToken) > 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(seedAdmin.Password))
            {
                logger.LogError(
                    "STARTUP CHECK FAILED: no active Super Administrator exists and no seed password is configured, so nobody can sign in to " +
                    "administer the system. Set Identity__SeedAdmin__Password (HMIS_SEED_ADMIN_PASSWORD in .env for Docker) and restart the API; " +
                    "the user '{UserName}' is then created with a forced password change at first sign-in.", seedAdmin.UserName);
            }
            else
            {
                logger.LogError(
                    "STARTUP CHECK FAILED: no active Super Administrator exists although a seed password is configured. The seed user name '{UserName}' " +
                    "is probably taken by an account that is inactive or not a Super Administrator. Reactivate it, or set Identity__SeedAdmin__UserName " +
                    "to a free user name and restart the API.", seedAdmin.UserName);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not verify that an active Super Administrator exists.");
        }
    }
}
