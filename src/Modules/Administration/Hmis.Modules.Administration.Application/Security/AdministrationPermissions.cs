using Hmis.BuildingBlocks.Application.Security;

namespace Hmis.Modules.Administration.Application.Security;

public static class AdministrationPermissions
{
    public const string Module = "Administration";

    public const string HospitalProfileView = "Admin.HospitalProfile.View";
    public const string HospitalProfileManage = "Admin.HospitalProfile.Manage";
    public const string DepartmentsView = "Admin.Departments.View";
    public const string DepartmentsManage = "Admin.Departments.Manage";
    public const string DesignationsView = "Admin.Designations.View";
    public const string DesignationsManage = "Admin.Designations.Manage";
    public const string SettingsManage = "Admin.Settings.Manage";
}

/// <summary>Defaults from docs/ROLE-PERMISSION-MATRIX.md §2. Reference data is viewable by every staff role.</summary>
public sealed class AdministrationPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    private static readonly string[] AllStaffRoles =
    [
        SystemRoles.HospitalAdministrator, SystemRoles.Receptionist, SystemRoles.Doctor, SystemRoles.Nurse,
        SystemRoles.Pharmacist, SystemRoles.LaboratoryTechnician, SystemRoles.Radiologist, SystemRoles.Accountant,
        SystemRoles.HrManager, SystemRoles.StoreManager, SystemRoles.HospitalOwner
    ];

    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(AdministrationPermissions.HospitalProfileView, AdministrationPermissions.Module, "View hospital profile and settings", AllStaffRoles),
        new(AdministrationPermissions.HospitalProfileManage, AdministrationPermissions.Module, "Edit hospital profile and logo",
            [SystemRoles.HospitalAdministrator]),
        new(AdministrationPermissions.DepartmentsView, AdministrationPermissions.Module, "View departments", AllStaffRoles),
        new(AdministrationPermissions.DepartmentsManage, AdministrationPermissions.Module, "Create, edit and deactivate departments",
            [SystemRoles.HospitalAdministrator]),
        new(AdministrationPermissions.DesignationsView, AdministrationPermissions.Module, "View designations", AllStaffRoles),
        new(AdministrationPermissions.DesignationsManage, AdministrationPermissions.Module, "Create, edit and deactivate designations",
            [SystemRoles.HospitalAdministrator, SystemRoles.HrManager]),
        new(AdministrationPermissions.SettingsManage, AdministrationPermissions.Module, "Manage system settings and number sequences", [])
    ];
}
