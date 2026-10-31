using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Patient.Application;
using Hmis.Modules.Patient.Domain;
using Hmis.Modules.Patient.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Hmis.Modules.Patient.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class PatientPresentationMarker;

public sealed class DocumentUploadForm
{
    public IFormFile? File { get; set; }
    public DocumentCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
}

[Route("api/v{version:apiVersion}/patients")]
public sealed class PatientsController(
    IPatientManager patients,
    IPatientMedicalManager medical,
    IPatientDocumentManager documents) : ApiControllerBase
{
    // ---------- Registration & demographics ----------

    /// <summary>Search by MR number, mobile, name or identification number.</summary>
    [HttpGet]
    [HasPermission(PatientPermissions.View)]
    [ProducesResponseType<ApiResponse<PagedResponse<PatientListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PatientSearchQuery query, CancellationToken cancellationToken) =>
        OkResponse(await patients.SearchAsync(query, cancellationToken));

    /// <summary>Possible existing records for the details being entered (warnings; exact ID match blocks registration).</summary>
    [HttpGet("duplicate-candidates")]
    [HasPermission(PatientPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DuplicateCandidateDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DuplicateCandidates([FromQuery] DuplicateCheckQuery query, CancellationToken cancellationToken) =>
        OkResponse(await patients.FindDuplicatesAsync(query, cancellationToken));

    [HttpGet("districts")]
    [HasPermission(PatientPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<string>>>(StatusCodes.Status200OK)]
    public IActionResult Districts() => OkResponse(BangladeshDistricts.All);

    [HttpGet("{id:guid}")]
    [HasPermission(PatientPermissions.View)]
    [ProducesResponseType<ApiResponse<PatientDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await patients.GetByIdAsync(id, cancellationToken));

    [HttpGet("by-mrn/{mrNumber}")]
    [HasPermission(PatientPermissions.View)]
    [ProducesResponseType<ApiResponse<PatientDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByMRNumber(string mrNumber, CancellationToken cancellationToken) =>
        OkResponse(await patients.GetByMRNumberAsync(mrNumber, cancellationToken));

    [HttpPost]
    [HasPermission(PatientPermissions.Create)]
    [ProducesResponseType<ApiResponse<PatientDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] PatientRegisterDto request, CancellationToken cancellationToken)
    {
        var patient = await patients.RegisterAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = patient.Id, version = "1" }, patient,
            $"Patient registered with MR number {patient.MRNumber}.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PatientPermissions.Update)]
    [ProducesResponseType<ApiResponse<PatientDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PatientUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await patients.UpdateAsync(id, request, cancellationToken), "Patient updated.");

    [HttpPatch("{id:guid}/status")]
    [HasPermission(PatientPermissions.Deactivate)]
    [ProducesResponseType<ApiResponse<PatientDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] PatientStatusUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await patients.SetStatusAsync(id, request, cancellationToken),
            request.IsActive ? "Patient record reactivated." : "Patient record deactivated.");

    // ---------- Photo ----------

    [HttpGet("{id:guid}/photo")]
    [HasPermission(PatientPermissions.View)]
    [Produces("image/png", "image/jpeg", "image/webp", "application/json")]
    public async Task<IActionResult> GetPhoto(Guid id, CancellationToken cancellationToken)
    {
        var photo = await patients.OpenPhotoAsync(id, cancellationToken);
        return photo is null ? NotFound() : PrivateFile(photo.Content, photo.ContentType, null);
    }

    [HttpPost("{id:guid}/photo")]
    [HasPermission(PatientPermissions.Update)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [ProducesResponseType<ApiResponse<PatientDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return OkResponse(await patients.UploadPhotoAsync(id, stream, file.Length, cancellationToken), "Photo updated.");
    }

    [HttpDelete("{id:guid}/photo")]
    [HasPermission(PatientPermissions.Update)]
    [ProducesResponseType<ApiResponse<PatientDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemovePhoto(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await patients.RemovePhotoAsync(id, cancellationToken), "Photo removed.");

    // ---------- Medical information (sensitive; views are audited) ----------

    [HttpGet("{id:guid}/medical-info")]
    [HasPermission(PatientPermissions.MedicalInfoView)]
    [ProducesResponseType<ApiResponse<MedicalInfoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedicalInfo(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await medical.GetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/allergies")]
    [HasPermission(PatientPermissions.MedicalInfoManage)]
    [ProducesResponseType<ApiResponse<AllergyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAllergy(Guid id, [FromBody] AllergyRequestDto request, CancellationToken cancellationToken) =>
        OkResponse(await medical.AddAllergyAsync(id, request, cancellationToken), "Allergy recorded.");

    [HttpPut("{id:guid}/allergies/{allergyId:guid}")]
    [HasPermission(PatientPermissions.MedicalInfoManage)]
    [ProducesResponseType<ApiResponse<AllergyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAllergy(Guid id, Guid allergyId, [FromBody] AllergyRequestDto request, CancellationToken cancellationToken) =>
        OkResponse(await medical.UpdateAllergyAsync(id, allergyId, request, cancellationToken), "Allergy updated.");

    [HttpPatch("{id:guid}/allergies/{allergyId:guid}/status")]
    [HasPermission(PatientPermissions.MedicalInfoManage)]
    [ProducesResponseType<ApiResponse<AllergyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAllergyStatus(Guid id, Guid allergyId, [FromBody] AllergyStatusDto request, CancellationToken cancellationToken) =>
        OkResponse(await medical.SetAllergyStatusAsync(id, allergyId, request, cancellationToken), "Allergy status updated.");

    [HttpPost("{id:guid}/medical-history")]
    [HasPermission(PatientPermissions.MedicalInfoManage)]
    [ProducesResponseType<ApiResponse<MedicalHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddHistory(Guid id, [FromBody] MedicalHistoryRequestDto request, CancellationToken cancellationToken) =>
        OkResponse(await medical.AddHistoryAsync(id, request, cancellationToken), "Medical history recorded.");

    [HttpPut("{id:guid}/medical-history/{historyId:guid}")]
    [HasPermission(PatientPermissions.MedicalInfoManage)]
    [ProducesResponseType<ApiResponse<MedicalHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateHistory(Guid id, Guid historyId, [FromBody] MedicalHistoryRequestDto request, CancellationToken cancellationToken) =>
        OkResponse(await medical.UpdateHistoryAsync(id, historyId, request, cancellationToken), "Medical history updated.");

    [HttpPost("{id:guid}/medical-history/{historyId:guid}/entered-in-error")]
    [HasPermission(PatientPermissions.MedicalInfoManage)]
    [ProducesResponseType<ApiResponse<MedicalHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkHistoryEnteredInError(Guid id, Guid historyId, CancellationToken cancellationToken) =>
        OkResponse(await medical.MarkHistoryEnteredInErrorAsync(id, historyId, cancellationToken), "Entry marked as entered in error.");

    // ---------- Documents ----------

    [HttpGet("{id:guid}/documents")]
    [HasPermission(PatientPermissions.DocumentsView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PatientDocumentDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocuments(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await documents.GetAsync(id, cancellationToken));

    /// <summary>Uploads a PDF, JPEG or PNG (max 10 MB; type checked from content).</summary>
    [HttpPost("{id:guid}/documents")]
    [HasPermission(PatientPermissions.DocumentsUpload)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType<ApiResponse<PatientDocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadDocument(Guid id, [FromForm] DocumentUploadForm form, CancellationToken cancellationToken)
    {
        if (form.File is null)
        {
            return BadRequest(ApiResponse.Fail("Choose a file to upload.",
                [new ApiError(ErrorCodes.ValidationError, "Choose a file to upload.", "file")], HttpContext.TraceIdentifier));
        }

        await using var stream = form.File.OpenReadStream();
        var document = await documents.UploadAsync(id, form.Category, form.Title, form.File.FileName, stream, form.File.Length, cancellationToken);
        return OkResponse(document, "Document uploaded.");
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/content")]
    [HasPermission(PatientPermissions.DocumentsView)]
    [Produces("application/pdf", "image/png", "image/jpeg", "application/json")]
    public async Task<IActionResult> GetDocumentContent(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await documents.OpenAsync(id, documentId, cancellationToken);
        return PrivateFile(document.Content, document.ContentType, document.FileName);
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [HasPermission(PatientPermissions.DocumentsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        await documents.RemoveAsync(id, documentId, cancellationToken);
        return NoContent();
    }

    /// <summary>Patient files are never cached by browsers or proxies and never content-sniffed.</summary>
    private FileStreamResult PrivateFile(Stream content, string contentType, string? fileName)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (fileName is not null)
        {
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = fileName }.ToString();
        }

        return File(content, contentType);
    }
}

[Route("api/v{version:apiVersion}/patient-merge-requests")]
public sealed class PatientMergeRequestsController(IPatientMergeManager merges) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PatientPermissions.MergeApprove)]
    [ProducesResponseType<ApiResponse<PagedResponse<MergeRequestDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] MergeRequestQuery query, CancellationToken cancellationToken) =>
        OkResponse(await merges.SearchAsync(query, cancellationToken));

    [HttpPost]
    [HasPermission(PatientPermissions.MergeRequest)]
    [ProducesResponseType<ApiResponse<MergeRequestDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] MergeRequestCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await merges.RequestAsync(request, cancellationToken), "Merge request submitted for approval.");

    [HttpPost("{id:guid}/approve")]
    [HasPermission(PatientPermissions.MergeApprove)]
    [ProducesResponseType<ApiResponse<MergeRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] MergeReviewDto request, CancellationToken cancellationToken) =>
        OkResponse(await merges.ApproveAsync(id, request, cancellationToken), "Records merged.");

    [HttpPost("{id:guid}/reject")]
    [HasPermission(PatientPermissions.MergeApprove)]
    [ProducesResponseType<ApiResponse<MergeRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] MergeReviewDto request, CancellationToken cancellationToken) =>
        OkResponse(await merges.RejectAsync(id, request, cancellationToken), "Merge request rejected.");
}
