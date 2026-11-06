using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Application.Seeding;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Application.Interfaces.Services;
using Hmis.Modules.Identity.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hmis.Modules.Identity.Application.Services;

/// <summary>
/// Demo logins for development and training installs (DemoData:Enabled): one user per front-line role, each linked
/// to the matching demo employee seeded by HR, all sharing the configured demo password with no forced change.
/// Idempotent (a username that already exists is left untouched) and skipped entirely when demo data is off.
/// </summary>
public sealed class IdentityDemoUsersSeedTask(
    IUserRepository users,
    IRoleRepository roles,
    IIdentityUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IOptions<DemoDataOptions> options,
    ILogger<IdentityDemoUsersSeedTask> logger) : IStartupTask
{
    private sealed record DemoUser(string UserName, string FullName, string Role, Guid EmployeeId);

    private static readonly DemoUser[] Users =
    [
        new("admin1", "Demo Administrator", SystemRoles.HospitalAdministrator, DemoDataIds.AdministratorEmployeeId),
        new("reception1", "Demo Receptionist", SystemRoles.Receptionist, DemoDataIds.ReceptionistEmployeeId),
        new("cashier1", "Demo Cashier", SystemRoles.Accountant, DemoDataIds.CashierEmployeeId),
        new("nurse1", "Demo Nurse", SystemRoles.Nurse, DemoDataIds.NurseEmployeeId),
        new("doctor1", "Dr. Demo Doctor", SystemRoles.Doctor, DemoDataIds.DoctorEmployeeId)
    ];

    public int Order => 40;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning("Demo logins not seeded: DemoData:Password is empty");
            return;
        }

        var created = 0;
        foreach (var demo in Users)
        {
            if (await users.UserNameExistsAsync(User.Normalize(demo.UserName), null, cancellationToken))
            {
                continue;
            }

            var role = await roles.GetByNormalizedNameAsync(User.Normalize(demo.Role), cancellationToken);
            if (role is null)
            {
                logger.LogWarning("Demo login {UserName} not seeded: role {Role} does not exist", demo.UserName, demo.Role);
                continue;
            }

            // The employee link is skipped if another login already owns that employee (unique per user).
            var employeeId = await users.EmployeeLinkedAsync(demo.EmployeeId, null, cancellationToken) ? null : (Guid?)demo.EmployeeId;

            var user = User.Create(demo.UserName, demo.FullName, $"{demo.UserName}@demo.local", null, employeeId,
                string.Empty, mustChangePassword: false);
            user.SetPassword(passwordHasher.Hash(user, settings.Password), mustChangePassword: false);
            user.SetRoles([role.Id]);
            await users.AddAsync(user, cancellationToken);
            created++;
        }

        if (created > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} demo logins (DemoData:Enabled); never enable this in production", created);
        }
    }
}
