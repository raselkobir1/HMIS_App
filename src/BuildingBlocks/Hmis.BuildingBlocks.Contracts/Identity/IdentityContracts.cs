namespace Hmis.BuildingBlocks.Contracts.Identity;

// Contracts owned and implemented by the Identity module.

public interface IUserEmployeeLookup
{
    /// <summary>The HR employee linked to a login account, or null when the account is not linked.</summary>
    Task<Guid?> GetEmployeeIdAsync(Guid userId, CancellationToken cancellationToken);
}
