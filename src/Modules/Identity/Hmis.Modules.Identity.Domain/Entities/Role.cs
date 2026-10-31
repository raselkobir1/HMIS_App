using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Identity.Domain.Entities;

[Audited]
public sealed class Role : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private readonly List<RolePermission> _permissions = [];

    private Role() { }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>System roles are seeded and cannot be renamed or deleted.</summary>
    public bool IsSystem { get; private set; }

    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions;

    public static Role Create(string name, string? description, bool isSystem = false)
    {
        var role = new Role { IsSystem = isSystem };
        role.SetName(name);
        role.Description = Clean(description);
        return role;
    }

    public void Update(string name, string? description)
    {
        if (IsSystem && !string.Equals(Name, name.Trim(), StringComparison.Ordinal))
        {
            throw new DomainException("ROLE_SYSTEM_RENAME", "System roles cannot be renamed.");
        }

        SetName(name);
        Description = Clean(description);
    }

    public bool HasPermission(Guid permissionId) => _permissions.Any(p => p.PermissionId == permissionId);

    public void SetPermissions(IEnumerable<Guid> permissionIds)
    {
        var target = permissionIds.ToHashSet();
        _permissions.RemoveAll(p => !target.Contains(p.PermissionId));
        foreach (var permissionId in target.Where(id => _permissions.All(p => p.PermissionId != id)))
        {
            _permissions.Add(new RolePermission(Id, permissionId));
        }
    }

    public void GrantPermission(Guid permissionId)
    {
        if (!HasPermission(permissionId))
        {
            _permissions.Add(new RolePermission(Id, permissionId));
        }
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("ROLE_NAME_REQUIRED", "Role name is required.");
        }

        Name = name.Trim();
        NormalizedName = User.Normalize(Name);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
