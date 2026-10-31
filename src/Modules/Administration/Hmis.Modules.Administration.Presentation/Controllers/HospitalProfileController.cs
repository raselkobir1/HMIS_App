using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Administration.Presentation.Controllers;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class AdministrationPresentationMarker;

[Route("api/v{version:apiVersion}/hospital-profile")]
public sealed class HospitalProfileController(IHospitalProfileManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AdministrationPermissions.HospitalProfileView)]
    [ProducesResponseType<ApiResponse<HospitalProfileDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetAsync(cancellationToken));

    [HttpPut]
    [HasPermission(AdministrationPermissions.HospitalProfileManage)]
    [ProducesResponseType<ApiResponse<HospitalProfileDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] HospitalProfileUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(request, cancellationToken), "Hospital profile updated.");

    /// <summary>Public hospital name and logo availability for the login page and header.</summary>
    [HttpGet("branding")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<BrandingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranding(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetBrandingAsync(cancellationToken));

    /// <summary>The hospital logo image (public).</summary>
    [HttpGet("logo")]
    [AllowAnonymous]
    [Produces("image/png", "image/jpeg", "image/webp", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken cancellationToken)
    {
        var logo = await manager.OpenLogoAsync(cancellationToken);
        if (logo is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(logo.Content, logo.ContentType);
    }

    /// <summary>Uploads a PNG, JPEG or WebP logo (max 1 MB). The type is checked from the file content.</summary>
    [HttpPost("logo")]
    [HasPermission(AdministrationPermissions.HospitalProfileManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [ProducesResponseType<ApiResponse<HospitalProfileDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return OkResponse(await manager.UploadLogoAsync(stream, file.Length, cancellationToken), "Logo updated.");
    }

    [HttpDelete("logo")]
    [HasPermission(AdministrationPermissions.HospitalProfileManage)]
    [ProducesResponseType<ApiResponse<HospitalProfileDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveLogo(CancellationToken cancellationToken) =>
        OkResponse(await manager.RemoveLogoAsync(cancellationToken), "Logo removed.");
}

[Route("api/v{version:apiVersion}/system-settings")]
public sealed class SystemSettingsController(ISystemSettingsManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AdministrationPermissions.HospitalProfileView)]
    [ProducesResponseType<ApiResponse<SystemSettingsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetAsync(cancellationToken));

    [HttpPut]
    [HasPermission(AdministrationPermissions.SettingsManage)]
    [ProducesResponseType<ApiResponse<SystemSettingsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromBody] SystemSettingsUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(request, cancellationToken), "Settings updated.");
}

[Route("api/v{version:apiVersion}/number-sequences")]
[HasPermission(AdministrationPermissions.SettingsManage)]
public sealed class NumberSequencesController(INumberSequenceManager manager) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<NumberSequenceDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetAllAsync(cancellationToken));

    [HttpPut("{key}")]
    [ProducesResponseType<ApiResponse<NumberSequenceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string key, [FromBody] NumberSequenceUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(key, request, cancellationToken), "Number format updated.");

    /// <summary>Moves the counter forward (e.g. to continue from a previous system). Never backwards.</summary>
    [HttpPut("{key}/next-value")]
    [ProducesResponseType<ApiResponse<NumberSequenceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetNextValue(string key, [FromBody] NumberSequenceNextValueDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetNextValueAsync(key, request, cancellationToken), "Next number updated.");
}
