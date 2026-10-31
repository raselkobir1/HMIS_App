using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Identity.Infrastructure.Services;

/// <summary>Resolves the HR employee linked to a login. Read from the database on each call, never cached in tokens.</summary>
public sealed class UserEmployeeLookup(IdentityDbContext context) : IUserEmployeeLookup
{
    public Task<Guid?> GetEmployeeIdAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.EmployeeId).FirstOrDefaultAsync(cancellationToken);
}
