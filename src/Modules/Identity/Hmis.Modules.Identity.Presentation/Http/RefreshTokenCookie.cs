using Microsoft.AspNetCore.Http;

namespace Hmis.Modules.Identity.Presentation.Http;

/// <summary>
/// The refresh token is only ever sent as an HttpOnly, Secure, SameSite=Strict cookie scoped to the auth endpoints,
/// so page scripts can never read it. The UI reaches the API same-origin (dev proxy / reverse proxy in production).
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "hmis_refresh";
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTime expiresAtUtc) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
            IsEssential = true
        });

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = Path });
}
