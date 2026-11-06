using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.Modules.Identity.Application.Interfaces.Repositories;
using Hmis.Modules.Identity.Domain.Entities;

namespace Hmis.Modules.Identity.Application.Services;

/// <summary>
/// IUserAccountService contract for other modules. HR calls it when an employee resigns or is terminated so the
/// person's login stops working the moment the employment record does.
/// </summary>
public sealed class UserAccountService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IIdentityUnitOfWork unitOfWork,
    SuperAdministratorGuard superAdminGuard,
    IDateTimeProvider dateTime,
    IAuditService auditService) : IUserAccountService
{
    public async Task DeactivateForEmployeeAsync(Guid employeeId, string reason, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetByEmployeeIdAsync(employeeId, cancellationToken);
        var active = users.Where(u => u.IsActive).ToList();
        if (active.Count == 0)
        {
            return;
        }

        var now = dateTime.UtcNow;
        var superAdminRole = await superAdminGuard.GetRoleAsync(cancellationToken);
        foreach (var user in active)
        {
            if (user.HasRole(superAdminRole.Id))
            {
                // The system must stay administrable even when the last administrator's employment ends.
                await superAdminGuard.EnsureNotLastSuperAdministratorAsync(superAdminRole.Id, user.Id, cancellationToken);
            }

            user.Deactivate();
            foreach (var token in await refreshTokenRepository.GetActiveByUserAsync(user.Id, now, cancellationToken))
            {
                token.Revoke(now, reason);
            }

            auditService.Record(new AuditEvent("Deactivated", "Identity", nameof(User), user.Id.ToString(),
                new { IsActive = true }, new { IsActive = false, Reason = reason, EmployeeId = employeeId }));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
