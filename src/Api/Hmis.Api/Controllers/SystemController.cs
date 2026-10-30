using System.Reflection;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Api.Controllers;

[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController(IWebHostEnvironment environment) : ApiControllerBase
{
    public sealed record SystemInfoDto(string Application, string Version, string Environment, DateTime ServerTimeUtc);

    /// <summary>Basic, non-sensitive service information.</summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public IActionResult GetInfo() =>
        OkResponse(new SystemInfoDto(
            "HMIS API",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            environment.EnvironmentName,
            DateTime.UtcNow));
}
