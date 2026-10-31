using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.BuildingBlocks.Presentation.Http;
using Hmis.Modules.Identity.Application.DTOs.Requests;
using Hmis.Modules.Identity.Application.DTOs.Responses;
using Hmis.Modules.Identity.Application.Interfaces.Managers;
using Hmis.Modules.Identity.Presentation.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hmis.Modules.Identity.Presentation.Controllers;

[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(IAuthManager authManager) : ApiControllerBase
{
    /// <summary>Signs in with username or email. Sets the refresh-token cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ApiResponse<AuthResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var session = await authManager.LoginAsync(request, cancellationToken);
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return OkResponse(session.Response, "Signed in successfully.");
    }

    /// <summary>Exchanges the refresh-token cookie for a new access token (token rotation).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<AuthResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var session = await authManager.RefreshAsync(RefreshTokenCookie.Read(Request), cancellationToken);
            RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
            return OkResponse(session.Response, "Session refreshed.");
        }
        catch
        {
            // A rejected refresh token must not linger in the browser.
            RefreshTokenCookie.Delete(Response);
            throw;
        }
    }

    /// <summary>Ends the current session and clears the refresh-token cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authManager.LogoutAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return OkResponse("Signed out.");
    }

    /// <summary>Changes the caller's password, signs out other sessions and issues a fresh session.</summary>
    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ApiResponse<AuthResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        var session = await authManager.ChangePasswordAsync(request, cancellationToken);
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return OkResponse(session.Response, "Password changed successfully.");
    }

    /// <summary>Current user profile with roles and effective permissions.</summary>
    [HttpGet("me")]
    [ProducesResponseType<ApiResponse<CurrentUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        OkResponse(await authManager.GetCurrentUserAsync(cancellationToken));
}
