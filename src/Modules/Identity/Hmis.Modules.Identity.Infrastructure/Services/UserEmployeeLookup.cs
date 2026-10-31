using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hmis.Modules.Identity.Infrastructure.Services;

/// <summary>Resolves the HR employee linked to a login. Read from the database on each call, never cached in tokens.</summary>
public sealed class UserEmployeeLookup(IdentityDbContext context) : IUserEmployeeLookup
{
    public Task<Guid?> GetEmployeeIdAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.EmployeeId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
}
