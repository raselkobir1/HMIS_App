using Hmis.BuildingBlocks.Application.Security;

namespace Hmis.Modules.Identity.Application.Security;

public static class IdentityPermissions
{
    public const string Module = "Identity";

    public const string UsersView = "Identity.Users.View";
    public const string UsersManage = "Identity.Users.Manage";
    public const string RolesView = "Identity.Roles.View";
    public const string RolesManage = "Identity.Roles.Manage";
    public const string LoginHistoryView = "Identity.LoginHistory.View";
}

/// <summary>Identity permissions and their default roles (docs/ROLE-PERMISSION-MATRIX.md §2).</summary>
public sealed class IdentityPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(IdentityPermissions.UsersView, IdentityPermissions.Module, "View users",
            [SystemRoles.HospitalAdministrator, SystemRoles.HrManager]),
        new(IdentityPermissions.UsersManage, IdentityPermissions.Module, "Create, update, activate/deactivate users and reset passwords",
            []),
        new(IdentityPermissions.RolesView, IdentityPermissions.Module, "View roles and permissions",
            [SystemRoles.HospitalAdministrator]),
        new(IdentityPermissions.RolesManage, IdentityPermissions.Module, "Manage roles and assign roles/permissions",
            []),
        new(IdentityPermissions.LoginHistoryView, IdentityPermissions.Module, "View login history",
            [SystemRoles.HospitalAdministrator])
    ];
}
