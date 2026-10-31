using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Clinical.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Clinical.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class ClinicalPresentationMarker;

/// <summary>OPD encounters: the encounter id is the checked-in appointment's id.</summary>
[Route("api/v{version:apiVersion}/opd/encounters")]
public sealed class OpdEncountersController(IClinicalManager manager) : ApiControllerBase
{
    /// <summary>Patient, allergies, history, vitals, the consultation and previous visits. Access is audited.</summary>
    [HttpGet("{encounterId:guid}")]
    [HasPermission(ClinicalPermissions.View)]
    [ProducesResponseType<ApiResponse<OpdWorkspaceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspace(Guid encounterId, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetOpdWorkspaceAsync(encounterId, cancellationToken));

    [HttpPost("{encounterId:guid}/vitals")]
    [HasPermission(ClinicalPermissions.VitalsRecord)]
    [ProducesResponseType<ApiResponse<VitalSignDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordVitals(Guid encounterId, [FromBody] VitalSignRequestDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.RecordOpdVitalsAsync(encounterId, request, cancellationToken), "Vitals recorded.");

    /// <summary>Starts (or reopens) the consultation for the encounter's own doctor.</summary>
    [HttpPost("{encounterId:guid}/consultation")]
    [HasPermission(ClinicalPermissions.ConsultationManage)]
    [ProducesResponseType<ApiResponse<ConsultationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> OpenConsultation(Guid encounterId, CancellationToken cancellationToken) =>
        OkResponse(await manager.OpenOpdConsultationAsync(encounterId, cancellationToken));
}

[Route("api/v{version:apiVersion}/consultations")]
public sealed class ConsultationsController(IClinicalManager manager) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [HasPermission(ClinicalPermissions.View)]
    [ProducesResponseType<ApiResponse<ConsultationRecordDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetConsultationAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(ClinicalPermissions.ConsultationManage)]
    [ProducesResponseType<ApiResponse<ConsultationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SaveDraft(Guid id, [FromBody] ConsultationSaveDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SaveDraftAsync(id, request, cancellationToken), "Draft saved.");

    [HttpPost("{id:guid}/finalize")]
    [HasPermission(ClinicalPermissions.ConsultationManage)]
    [ProducesResponseType<ApiResponse<ConsultationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> FinalizeConsultation(Guid id, [FromBody] FinalizeDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.FinalizeAsync(id, request, cancellationToken), "Consultation finalized and visit completed.");

    [HttpPost("{id:guid}/addenda")]
    [HasPermission(ClinicalPermissions.ConsultationManage)]
    [ProducesResponseType<ApiResponse<ConsultationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAddendum(Guid id, [FromBody] AddendumCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.AddAddendumAsync(id, request, cancellationToken), "Addendum added.");
}

[Route("api/v{version:apiVersion}/vital-signs")]
public sealed class VitalSignsController(IClinicalManager manager) : ApiControllerBase
{
    [HttpPost("{id:guid}/entered-in-error")]
    [HasPermission(ClinicalPermissions.VitalsRecord)]
    [ProducesResponseType<ApiResponse<VitalSignDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkEnteredInError(Guid id, [FromBody] EnteredInErrorDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.MarkVitalsInErrorAsync(id, request, cancellationToken), "Vitals marked as entered in error.");
}

[Route("api/v{version:apiVersion}/patients/{patientId:guid}/consultations")]
public sealed class PatientConsultationsController(IClinicalManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(ClinicalPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ConsultationSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(Guid patientId, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetPatientHistoryAsync(patientId, cancellationToken));
}
