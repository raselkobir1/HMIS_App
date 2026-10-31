using Hmis.BuildingBlocks.Domain.Entities;

namespace Hmis.Modules.Identity.Domain.Entities;

/// <summary>
/// Rotating refresh token. Only the SHA-256 hash is stored. All tokens issued from one login share a FamilyId;
/// presenting an already-rotated token revokes the whole family (token theft detection).
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Issue(Guid userId, string tokenHash, Guid familyId, DateTime utcNow, TimeSpan lifetime, string? ipAddress) =>
        new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAt = utcNow,
            ExpiresAt = utcNow.Add(lifetime),
            CreatedByIp = ipAddress
        };

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTime utcNow) => ExpiresAt <= utcNow;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string reason, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = utcNow;
        RevokedReason = reason;
        ReplacedByTokenId = replacedByTokenId;
    }
}
