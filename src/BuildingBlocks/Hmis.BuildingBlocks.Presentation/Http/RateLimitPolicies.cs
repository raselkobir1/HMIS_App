namespace Hmis.BuildingBlocks.Presentation.Http;

public static class RateLimitPolicies
{
    /// <summary>Fixed window per IP for authentication endpoints (login, refresh).</summary>
    public const string Auth = "auth";
}
