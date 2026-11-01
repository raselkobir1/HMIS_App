using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Nursing.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Nursing.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class NursingPresentationMarker;

[Route("api/v{version:apiVersion}/nursing")]
public sealed class NursingWardsController(IWardNursingManager manager) : ApiControllerBase
{
    /// <summary>The shift now and the wards the signed-in nurse is rostered to.</summary>
    [HttpGet("current-shift")]
    [HasPermission(NursingPermissions.View)]
    [ProducesResponseType<ApiResponse<CurrentShiftDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentShift(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetCurrentShiftAsync(cancellationToken));

    [HttpGet("wards/{wardId:guid}")]
    [HasPermission(NursingPermissions.View)]
    [ProducesResponseType<ApiResponse<WardRoundDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWard(Guid wardId, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetWardAsync(wardId, cancellationToken));

    [HttpGet("admissions/{admissionId:guid}")]
    [HasPermission(NursingPermissions.View)]
    [ProducesResponseType<ApiResponse<NursingPatientDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatient(Guid admissionId, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetPatientAsync(admissionId, cancellationToken));
}

[Route("api/v{version:apiVersion}/nursing/admissions/{admissionId:guid}")]
public sealed class NursingRecordsController(IMarManager mar, INursingRecordManager records) : ApiControllerBase
{
    /// <summary>The MAR for one hospital-local day (defaults to today). Access is audited.</summary>
    [HttpGet("mar")]
    [HasPermission(NursingPermissions.View)]
    [ProducesResponseType<ApiResponse<MarDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMar(Guid admissionId, [FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        OkResponse(await mar.GetAsync(admissionId, date, cancellationToken));

    [HttpPost("mar")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<AdministrationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordDose(Guid admissionId, [FromBody] AdministrationCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await mar.RecordAsync(admissionId, request, cancellationToken), "Dose recorded.");

    /// <summary>One fluid day, 8am to 8am (defaults to the current one). Access is audited.</summary>
    [HttpGet("intake-output")]
    [HasPermission(NursingPermissions.View)]
    [ProducesResponseType<ApiResponse<IntakeOutputDayDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIntakeOutput(Guid admissionId, [FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        OkResponse(await records.GetIntakeOutputAsync(admissionId, date, cancellationToken));

    [HttpPost("intake-output")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<IntakeOutputEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordIntakeOutput(Guid admissionId, [FromBody] IntakeOutputCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await records.RecordIntakeOutputAsync(admissionId, request, cancellationToken), "Entry recorded.");

    [HttpGet("notes")]
    [HasPermission(NursingPermissions.View)]
    [ProducesResponseType<ApiResponse<NursingNotesDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotes(Guid admissionId, CancellationToken cancellationToken) =>
        OkResponse(await records.GetNotesAsync(admissionId, cancellationToken));

    [HttpPost("notes")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<NursingNoteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddNote(Guid admissionId, [FromBody] NursingNoteCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await records.AddNoteAsync(admissionId, request, cancellationToken), "Note saved.");
}

/// <summary>Corrections: the author marks an entry as entered in error (with a reason); nothing is deleted.</summary>
[Route("api/v{version:apiVersion}/nursing")]
public sealed class NursingCorrectionsController(IMarManager mar, INursingRecordManager records) : ApiControllerBase
{
    [HttpPost("mar/{id:guid}/entered-in-error")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<AdministrationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkDoseInError(Guid id, [FromBody] EnteredInErrorDto request, CancellationToken cancellationToken) =>
        OkResponse(await mar.MarkInErrorAsync(id, request, cancellationToken), "Entry marked as entered in error.");

    [HttpPost("intake-output/{id:guid}/entered-in-error")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<IntakeOutputEntryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkFluidInError(Guid id, [FromBody] EnteredInErrorDto request, CancellationToken cancellationToken) =>
        OkResponse(await records.MarkIntakeOutputInErrorAsync(id, request, cancellationToken), "Entry marked as entered in error.");

    [HttpPost("notes/{id:guid}/entered-in-error")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<NursingNoteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkNoteInError(Guid id, [FromBody] EnteredInErrorDto request, CancellationToken cancellationToken) =>
        OkResponse(await records.MarkNoteInErrorAsync(id, request, cancellationToken), "Note marked as entered in error.");
}

[Route("api/v{version:apiVersion}/nursing")]
public sealed class NursingShiftsController(IShiftManager manager) : ApiControllerBase
{
    [HttpGet("handovers")]
    [HasPermission(NursingPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<HandoverDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHandovers([FromQuery] HandoverQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetHandoversAsync(query, cancellationToken));

    /// <summary>Creates the ward's handover for the shift, or revises the author's own one until it is received.</summary>
    [HttpPost("handovers")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<HandoverDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SaveHandover([FromBody] HandoverSaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SaveHandoverAsync(request, cancellationToken), "Handover saved.");

    [HttpPost("handovers/{id:guid}/receive")]
    [HasPermission(NursingPermissions.Record)]
    [ProducesResponseType<ApiResponse<HandoverDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReceiveHandover(Guid id, [FromBody] HandoverReceiveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.ReceiveHandoverAsync(id, request, cancellationToken), "Handover received.");

    /// <summary>Nursing.View or Nursing.Roster.Manage (checked in the manager).</summary>
    [HttpGet("roster")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RosterEntryDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoster([FromQuery] RosterQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetRosterAsync(query, cancellationToken));

    [HttpPost("roster")]
    [HasPermission(NursingPermissions.RosterManage)]
    [ProducesResponseType<ApiResponse<RosterEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddRosterEntry([FromBody] RosterCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.AddRosterEntryAsync(request, cancellationToken), "Nurse assigned.");

    [HttpDelete("roster/{id:guid}")]
    [HasPermission(NursingPermissions.RosterManage)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveRosterEntry(Guid id, CancellationToken cancellationToken)
    {
        await manager.RemoveRosterEntryAsync(id, cancellationToken);
        return OkResponse("Assignment removed.");
    }
}
