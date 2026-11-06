using System.Reflection;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Api.Controllers;

[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController : ApiControllerBase
{
    public sealed record SystemInfoDto(string Application, string Version, DateTime ServerTimeUtc);

    /// <summary>
    /// Basic, non-sensitive service information. Anonymous so monitoring can probe it, which is why the environment
    /// name (Development enables Swagger and demo logins) is deliberately not included.
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public IActionResult GetInfo() =>
        OkResponse(new SystemInfoDto(
            "HMIS API",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            DateTime.UtcNow));
}
