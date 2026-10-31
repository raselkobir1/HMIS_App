namespace Hmis.Modules.Identity.Application.Options;

/// <summary>Account lockout policy (configuration section "Identity:Security").</summary>
public sealed class AccountSecurityOptions
{
    public const string SectionName = "Identity:Security";

    public int MaxFailedAccessAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}

/// <summary>
/// Initial Super Administrator created on first start when none exists (configuration section "Identity:SeedAdmin").
/// The password must come from user-secrets or the environment variable Identity__SeedAdmin__Password.
/// </summary>
public sealed class SeedAdminOptions
{
    public const string SectionName = "Identity:SeedAdmin";

    public string UserName { get; set; } = "superadmin";
    public string FullName { get; set; } = "Super Administrator";
    public string? Email { get; set; }
    public string? Password { get; set; }
}
