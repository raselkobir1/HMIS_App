namespace Hmis.BuildingBlocks.Contracts.Identity;

// Contracts owned and implemented by the Identity module.

public interface IUserEmployeeLookup
{
    /// <summary>The HR employee linked to a login account, or null when the account is not linked.</summary>
    Task<Guid?> GetEmployeeIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Display names of login accounts (e.g. "recorded by" on clinical entries). Unknown ids are omitted.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

/// <summary>Login-account actions other modules trigger (e.g. HR when an employee leaves).</summary>
public interface IUserAccountService
{
    /// <summary>
    /// Deactivates the login account(s) linked to the employee, signs them out (refresh tokens revoked) and audits the
    /// reason. No-op when no account is linked or it is already inactive. Reactivation is never automatic: an
    /// administrator re-enables the login deliberately.
    /// </summary>
    Task DeactivateForEmployeeAsync(Guid employeeId, string reason, CancellationToken cancellationToken);
}

/// <summary>Finds login accounts to address (e.g. notification recipients). Only active accounts are returned.</summary>
public interface IUserDirectory
{
    /// <summary>Active users holding the permission through a role or a direct grant.</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsWithPermissionAsync(string permission, CancellationToken cancellationToken);

    /// <summary>Active users linked to the HR employee (normally one).</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
}
