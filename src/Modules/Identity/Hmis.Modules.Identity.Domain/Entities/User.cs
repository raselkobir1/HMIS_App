using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Identity.Domain.Entities;

[Audited]
public sealed class User : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private readonly List<UserRole> _roles = [];
    private readonly List<UserPermission> _permissions = [];

    private User() { }

    public string UserName { get; private set; } = string.Empty;
    public string NormalizedUserName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }

    [NotAudited]
    public string PasswordHash { get; private set; } = string.Empty;

    [NotAudited]
    public string SecurityStamp { get; private set; } = NewStamp();

    public bool IsActive { get; private set; } = true;
    public bool MustChangePassword { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>Optional link to hr.Employees (no cross-schema FK; validated through a contract).</summary>
    public Guid? EmployeeId { get; private set; }

    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => _roles;
    public IReadOnlyCollection<UserPermission> Permissions => _permissions;

    public static User Create(
        string userName,
        string fullName,
        string? email,
        string? phoneNumber,
        Guid? employeeId,
        string passwordHash,
        bool mustChangePassword)
    {
        var user = new User { PasswordHash = passwordHash, MustChangePassword = mustChangePassword };
        user.SetUserName(userName);
        user.UpdateProfile(fullName, email, phoneNumber, employeeId);
        return user;
    }

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public void UpdateProfile(string fullName, string? email, string? phoneNumber, Guid? employeeId)
    {
        FullName = fullName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        NormalizedEmail = Email is null ? null : Normalize(Email);
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        EmployeeId = employeeId;
    }

    /// <summary>Sets a new password hash. Rotates the security stamp so existing sessions can be invalidated.</summary>
    public void SetPassword(string passwordHash, bool mustChangePassword)
    {
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
        SecurityStamp = NewStamp();
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEnd.HasValue && LockoutEnd.Value > utcNow;

    public void RecordFailedLogin(DateTime utcNow, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= maxFailedAttempts)
        {
            LockoutEnd = utcNow.Add(lockoutDuration);
            AccessFailedCount = 0;
        }
    }

    public void RecordSuccessfulLogin(DateTime utcNow)
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
        LastLoginAt = utcNow;
    }

    public void Unlock()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    public void Activate() => IsActive = true;

    public void Deactivate()
    {
        IsActive = false;
        SecurityStamp = NewStamp();
    }

    public bool HasRole(Guid roleId) => _roles.Any(r => r.RoleId == roleId);

    public void SetRoles(IEnumerable<Guid> roleIds)
    {
        var target = roleIds.ToHashSet();
        _roles.RemoveAll(r => !target.Contains(r.RoleId));
        foreach (var roleId in target.Where(id => _roles.All(r => r.RoleId != id)))
        {
            _roles.Add(new UserRole(Id, roleId));
        }
    }

    public void SetDirectPermissions(IEnumerable<Guid> permissionIds)
    {
        var target = permissionIds.ToHashSet();
        _permissions.RemoveAll(p => !target.Contains(p.PermissionId));
        foreach (var permissionId in target.Where(id => _permissions.All(p => p.PermissionId != id)))
        {
            _permissions.Add(new UserPermission(Id, permissionId));
        }
    }

    private void SetUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new DomainException("USERNAME_REQUIRED", "Username is required.");
        }

        UserName = userName.Trim();
        NormalizedUserName = Normalize(UserName);
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
