using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Ipd.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Ipd.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class IpdPresentationMarker;

[Route("api/v{version:apiVersion}/ipd")]
public sealed class IpdSetupController(IBedSetupManager manager) : ApiControllerBase
{
    [HttpGet("bed-categories")]
    [HasPermission(IpdPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<BedCategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetCategoriesAsync(cancellationToken));

    [HttpPost("bed-categories")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<BedCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCategory([FromBody] BedCategorySaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CreateCategoryAsync(request, cancellationToken), "Bed category added.");

    [HttpPut("bed-categories/{id:guid}")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<BedCategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] BedCategorySaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateCategoryAsync(id, request, cancellationToken), "Bed category updated.");

    [HttpPatch("bed-categories/{id:guid}/active")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<BedCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCategoryActive(Guid id, [FromBody] ActiveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetCategoryActiveAsync(id, request, cancellationToken));

    [HttpGet("wards")]
    [HasPermission(IpdPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<WardDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWards(CancellationToken cancellationToken) => OkResponse(await manager.GetWardsAsync(cancellationToken));

    [HttpPost("wards")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<WardDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWard([FromBody] WardSaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CreateWardAsync(request, cancellationToken), "Ward added.");

    [HttpPut("wards/{id:guid}")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<WardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateWard(Guid id, [FromBody] WardSaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateWardAsync(id, request, cancellationToken), "Ward updated.");

    [HttpPatch("wards/{id:guid}/active")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<WardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetWardActive(Guid id, [FromBody] ActiveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetWardActiveAsync(id, request, cancellationToken));

    [HttpGet("beds")]
    [HasPermission(IpdPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<BedDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeds([FromQuery] BedQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetBedsAsync(query, cancellationToken));

    [HttpPost("beds")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<BedDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBed([FromBody] BedSaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CreateBedAsync(request, cancellationToken), "Bed added.");

    [HttpPut("beds/{id:guid}")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<BedDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBed(Guid id, [FromBody] BedSaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateBedAsync(id, request, cancellationToken), "Bed updated.");

    [HttpPatch("beds/{id:guid}/active")]
    [HasPermission(IpdPermissions.SetupManage)]
    [ProducesResponseType<ApiResponse<BedDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetBedActive(Guid id, [FromBody] ActiveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetBedActiveAsync(id, request, cancellationToken));

    /// <summary>Housekeeping: cleaned, under maintenance or reserved. Occupancy changes only through admissions.</summary>
    [HttpPatch("beds/{id:guid}/status")]
    [HasPermission(IpdPermissions.BedsHousekeeping)]
    [ProducesResponseType<ApiResponse<BedDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetBedStatus(Guid id, [FromBody] BedStatusDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetBedStatusAsync(id, request, cancellationToken), "Bed status updated.");

    [HttpGet("bed-board")]
    [HasPermission(IpdPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<WardBoardDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoard(CancellationToken cancellationToken) => OkResponse(await manager.GetBoardAsync(cancellationToken));
}

[Route("api/v{version:apiVersion}/ipd/admissions")]
public sealed class AdmissionsController(IAdmissionManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(IpdPermissions.View)]
    [ProducesResponseType<ApiResponse<PagedResponse<AdmissionSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] AdmissionListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(IpdPermissions.View)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) => OkResponse(await manager.GetAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(IpdPermissions.Admit)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Admit([FromBody] AdmitDto request, CancellationToken cancellationToken)
    {
        var admission = await manager.AdmitAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = admission.Summary.Id, version = "1" }, admission, "Patient admitted.");
    }

    [HttpPost("{id:guid}/transfer")]
    [HasPermission(IpdPermissions.Admit)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.TransferAsync(id, request, cancellationToken), "Patient transferred.");

    [HttpPost("{id:guid}/doctor")]
    [HasPermission(IpdPermissions.Admit)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeDoctor(Guid id, [FromBody] ChangeDoctorDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.ChangeDoctorAsync(id, request, cancellationToken), "Attending doctor changed.");

    /// <summary>Posts bed rent due so far (the periodic job does this too). Billing staff only, like the final bill.</summary>
    [HttpPost("{id:guid}/charges/sync")]
    [HasPermission(IpdPermissions.DischargeComplete)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncCharges(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.SyncChargesAsync(id, cancellationToken), "Charges updated.");

    /// <summary>Doctor's discharge order with the discharge summary (create or amend).</summary>
    [HttpPut("{id:guid}/discharge-order")]
    [HasPermission(IpdPermissions.DischargeOrder)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> OrderDischarge(Guid id, [FromBody] DischargeOrderDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.OrderDischargeAsync(id, request, cancellationToken), "Discharge ordered.");

    [HttpPost("{id:guid}/discharge-order/cancel")]
    [HasPermission(IpdPermissions.DischargeOrder)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelDischargeOrder(Guid id, [FromBody] AdmissionRowVersionDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CancelDischargeOrderAsync(id, request, cancellationToken), "Discharge order cancelled.");

    /// <summary>Fixes the discharge time, posts the last bed days and finalizes the bill for settlement.</summary>
    [HttpPost("{id:guid}/final-bill")]
    [HasPermission(IpdPermissions.DischargeComplete)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PrepareFinalBill(Guid id, [FromBody] AdmissionRowVersionDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.PrepareFinalBillAsync(id, request, cancellationToken), "Final bill prepared.");

    /// <summary>Administrator's correction after the final bill: the patient stays. The final invoice stays payable; later charges go to a supplementary bill.</summary>
    [HttpPost("{id:guid}/reopen")]
    [HasPermission(IpdPermissions.DischargeReopen)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.ReopenAsync(id, request, cancellationToken), "Stay reopened.");

    /// <summary>Frees the bed. Needs a settled bill, or an override reason from an administrator.</summary>
    [HttpPost("{id:guid}/release")]
    [HasPermission(IpdPermissions.DischargeComplete)]
    [ProducesResponseType<ApiResponse<AdmissionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Release(Guid id, [FromBody] ReleaseDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.ReleaseAsync(id, request, cancellationToken), "Patient discharged.");

    /// <summary>Printable discharge summary (clinical access; audited).</summary>
    [HttpGet("{id:guid}/discharge-summary")]
    [HasPermission(IpdPermissions.View)]
    [ProducesResponseType<ApiResponse<DischargeSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDischargeSummary(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetDischargeSummaryAsync(id, cancellationToken));
}
