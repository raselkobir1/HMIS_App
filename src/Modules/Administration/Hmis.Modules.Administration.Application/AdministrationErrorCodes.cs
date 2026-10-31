namespace Hmis.Modules.Administration.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class AdministrationApplicationMarker;

public static class AdministrationErrorCodes
{
    public const string LogoInvalid = "LOGO_INVALID";
    public const string DepartmentNotFound = "DEPARTMENT_NOT_FOUND";
    public const string DepartmentCodeTaken = "DEPARTMENT_CODE_TAKEN";
    public const string DepartmentNameTaken = "DEPARTMENT_NAME_TAKEN";
    public const string DepartmentHasChildren = "DEPARTMENT_HAS_SUB_DEPARTMENTS";
    public const string DepartmentHasActiveChildren = "DEPARTMENT_HAS_ACTIVE_SUB_DEPARTMENTS";
    public const string DepartmentParentInactive = "DEPARTMENT_PARENT_INACTIVE";
    public const string DesignationNameTaken = "DESIGNATION_NAME_TAKEN";
}
