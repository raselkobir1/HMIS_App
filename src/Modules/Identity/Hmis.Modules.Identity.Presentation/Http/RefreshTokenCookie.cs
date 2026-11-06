using Microsoft.AspNetCore.Http;

namespace Hmis.Modules.Identity.Presentation.Http;

/// <summary>
/// The refresh token is only ever sent as an HttpOnly, SameSite=Strict cookie scoped to the auth endpoints,
/// so page scripts can never read it. The UI reaches the API same-origin (dev proxy / reverse proxy in production).
/// The Secure flag follows the request scheme: it is set whenever the request arrived over HTTPS, directly or through
/// a TLS-terminating reverse proxy that sends X-Forwarded-Proto. Over plain HTTP the cookie is not marked Secure,
/// otherwise browsers would drop it and every session would end when the access token expires.
/// Production must terminate TLS in front of the UI so the cookie is always Secure there (see docs/SECURITY.md).
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "hmis_refresh";
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpContext context, string token, DateTime expiresAtUtc) =>
        context.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
            IsEssential = true
        });

    public static void Delete(HttpContext context) =>
        context.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = Path
        });
}
