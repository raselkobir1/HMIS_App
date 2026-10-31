namespace Hmis.Modules.Identity.Application.Models;

public sealed record UserAccessSnapshot(
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyCollection<string> PermissionCodes);

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public sealed record GeneratedRefreshToken(string Token, string TokenHash);

public enum PasswordCheckResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}
