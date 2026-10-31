namespace Hmis.Modules.Identity.Domain.Entities;

public sealed class UserRole(Guid userId, Guid roleId)
{
    public Guid UserId { get; private set; } = userId;
    public Guid RoleId { get; private set; } = roleId;
}

public sealed class RolePermission(Guid roleId, Guid permissionId)
{
    public Guid RoleId { get; private set; } = roleId;
    public Guid PermissionId { get; private set; } = permissionId;
}

/// <summary>Permission granted directly to a user in addition to their roles.</summary>
public sealed class UserPermission(Guid userId, Guid permissionId)
{
    public Guid UserId { get; private set; } = userId;
    public Guid PermissionId { get; private set; } = permissionId;
}
