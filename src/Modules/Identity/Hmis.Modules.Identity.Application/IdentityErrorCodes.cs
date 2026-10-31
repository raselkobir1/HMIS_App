namespace Hmis.Modules.Identity.Application;

public static class IdentityErrorCodes
{
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string InvalidCurrentPassword = "INVALID_CURRENT_PASSWORD";
    public const string PasswordReused = "PASSWORD_SAME_AS_CURRENT";
    public const string UserNameTaken = "USERNAME_TAKEN";
    public const string EmailTaken = "EMAIL_TAKEN";
    public const string RoleNameTaken = "ROLE_NAME_TAKEN";
    public const string RoleNotFound = "ROLE_NOT_FOUND";
    public const string PermissionNotFound = "PERMISSION_NOT_FOUND";
    public const string CannotChangeOwnAccount = "CANNOT_CHANGE_OWN_ACCOUNT";
    public const string LastSuperAdministrator = "LAST_SUPER_ADMINISTRATOR";
    public const string SuperAdministratorRequired = "SUPER_ADMINISTRATOR_REQUIRED";
    public const string SuperAdministratorPermissionsRequired = "SUPER_ADMINISTRATOR_PERMISSIONS_REQUIRED";
    public const string SystemRoleDelete = "ROLE_SYSTEM_DELETE";
    public const string RoleInUse = "ROLE_IN_USE";
    public const string EmployeeInvalid = "EMPLOYEE_INVALID";
    public const string EmployeeAlreadyLinked = "EMPLOYEE_ALREADY_LINKED";
}
