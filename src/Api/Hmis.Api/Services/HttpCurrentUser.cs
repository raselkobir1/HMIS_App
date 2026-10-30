using System.Security.Claims;
using Hmis.BuildingBlocks.Application.Abstractions;

namespace Hmis.Api.Services;

/// <summary>ICurrentUser backed by the authenticated JWT principal. Managers depend on ICurrentUser, never on HttpContext.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub"), out var id)
            ? id
            : null;

    public string? UserName => Principal?.FindFirstValue("unique_name") ?? Principal?.Identity?.Name;
}
