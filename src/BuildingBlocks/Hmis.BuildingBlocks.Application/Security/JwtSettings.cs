namespace Hmis.BuildingBlocks.Application.Security;

/// <summary>Shared by the API host (token validation) and the Identity module (token issuing).</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Secret: supplied by environment variable Jwt__SigningKey or user-secrets, never appsettings.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
