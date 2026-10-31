namespace Hmis.Modules.Identity.Domain.Constants;

public static class LoginFailureReasons
{
    public const string UnknownUser = "UnknownUser";
    public const string InvalidPassword = "InvalidPassword";
    public const string LockedOut = "LockedOut";
    public const string Inactive = "Inactive";
}
