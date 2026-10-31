using Hmis.BuildingBlocks.Domain.Entities;

namespace Hmis.Modules.Identity.Domain.Entities;

/// <summary>Append-only record of every login attempt.</summary>
public sealed class LoginHistory : BaseEntity
{
    private LoginHistory() { }

    public Guid? UserId { get; private set; }
    public string UserNameAttempted { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public string? FailureReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime AttemptedAt { get; private set; }

    public static LoginHistory Record(
        Guid? userId,
        string userNameAttempted,
        bool succeeded,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        DateTime utcNow) =>
        new()
        {
            UserId = userId,
            UserNameAttempted = userNameAttempted.Length > 256 ? userNameAttempted[..256] : userNameAttempted,
            Succeeded = succeeded,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent,
            AttemptedAt = utcNow
        };
}
