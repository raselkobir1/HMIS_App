using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Files;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Patient.Domain.Entities;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Application;

/// <summary>Allergies and baseline medical history. Reading is audited (sensitive data); nothing is ever deleted.</summary>
public sealed class PatientMedicalManager(
    IPatientRepository patients,
    IPatientAllergyRepository allergies,
    IPatientMedicalHistoryRepository histories,
    IPatientUnitOfWork unitOfWork,
    IAuditService auditService) : IPatientMedicalManager, IPatientClinicalSummary
{
    /// <summary>Contract for Clinical: active allergies and valid history entries (Clinical audits its own access).</summary>
    async Task<PatientClinicalSummary> IPatientClinicalSummary.GetAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var allergyList = await allergies.GetForPatientAsync(patientId, cancellationToken);
        var historyList = await histories.GetForPatientAsync(patientId, cancellationToken);
        return new PatientClinicalSummary(
            allergyList.Where(a => a.Status == AllergyStatus.Active)
                .Select(a => new PatientAllergyAlert(a.Allergen, a.Type.ToString(), a.Severity.ToString(), a.Reaction)).ToList(),
            historyList.Where(h => !h.IsEnteredInError)
                .Select(h => new PatientHistoryItem(h.Category.ToString(), h.Description, h.OnsetDate)).ToList());
    }

    public async Task<MedicalInfoDto> GetAsync(Guid patientId, CancellationToken cancellationToken)
    {
        await GetPatientAsync(patientId, cancellationToken);

        // Access to sensitive clinical data is itself recorded (BRD §11).
        await auditService.RecordImmediateAsync(
            new AuditEvent("View", "Patient", "PatientMedicalInfo", patientId.ToString()), cancellationToken);

        var allergyList = await allergies.GetForPatientAsync(patientId, cancellationToken);
        var historyList = await histories.GetForPatientAsync(patientId, cancellationToken);
        return new MedicalInfoDto(allergyList.Select(ToDto).ToList(), historyList.Select(ToDto).ToList());
    }

    public async Task<AllergyDto> AddAllergyAsync(Guid patientId, AllergyRequestDto request, CancellationToken cancellationToken)
    {
        (await GetPatientAsync(patientId, cancellationToken)).EnsureEditable();
        var allergy = PatientAllergy.Record(patientId, request.Allergen, request.Type, request.Severity, request.Reaction);
        await allergies.AddAsync(allergy, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(allergy);
    }

    public async Task<AllergyDto> UpdateAllergyAsync(Guid patientId, Guid allergyId, AllergyRequestDto request, CancellationToken cancellationToken)
    {
        (await GetPatientAsync(patientId, cancellationToken)).EnsureEditable();
        var allergy = await GetAllergyAsync(patientId, allergyId, cancellationToken);
        allergies.SetExpectedRowVersion(allergy, request.RowVersion);
        allergy.Update(request.Allergen, request.Type, request.Severity, request.Reaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(allergy);
    }

    public async Task<AllergyDto> SetAllergyStatusAsync(Guid patientId, Guid allergyId, AllergyStatusDto request, CancellationToken cancellationToken)
    {
        (await GetPatientAsync(patientId, cancellationToken)).EnsureEditable();
        var allergy = await GetAllergyAsync(patientId, allergyId, cancellationToken);
        allergy.ChangeStatus(request.Status);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(allergy);
    }

    public async Task<MedicalHistoryDto> AddHistoryAsync(Guid patientId, MedicalHistoryRequestDto request, CancellationToken cancellationToken)
    {
        (await GetPatientAsync(patientId, cancellationToken)).EnsureEditable();
        var entry = PatientMedicalHistory.Record(patientId, request.Category, request.Description, request.OnsetDate);
        await histories.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<MedicalHistoryDto> UpdateHistoryAsync(Guid patientId, Guid historyId, MedicalHistoryRequestDto request, CancellationToken cancellationToken)
    {
        (await GetPatientAsync(patientId, cancellationToken)).EnsureEditable();
        var entry = await GetHistoryAsync(patientId, historyId, cancellationToken);
        histories.SetExpectedRowVersion(entry, request.RowVersion);
        entry.Update(request.Category, request.Description, request.OnsetDate);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<MedicalHistoryDto> MarkHistoryEnteredInErrorAsync(Guid patientId, Guid historyId, CancellationToken cancellationToken)
    {
        (await GetPatientAsync(patientId, cancellationToken)).EnsureEditable();
        var entry = await GetHistoryAsync(patientId, historyId, cancellationToken);
        entry.MarkEnteredInError();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    private async Task<PatientEntity> GetPatientAsync(Guid id, CancellationToken cancellationToken) =>
        await patients.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Patient", id);

    private async Task<PatientAllergy> GetAllergyAsync(Guid patientId, Guid id, CancellationToken cancellationToken) =>
        await allergies.GetByIdAsync(id, cancellationToken) is { } a && a.PatientId == patientId
            ? a
            : throw new NotFoundException("Allergy", id);

    private async Task<PatientMedicalHistory> GetHistoryAsync(Guid patientId, Guid id, CancellationToken cancellationToken) =>
        await histories.GetByIdAsync(id, cancellationToken) is { } h && h.PatientId == patientId
            ? h
            : throw new NotFoundException("Medical history entry", id);

    private static AllergyDto ToDto(PatientAllergy a) =>
        new(a.Id, a.Allergen, a.Type, a.Severity, a.Reaction, a.Status, a.CreatedAt, a.RowVersion);

    private static MedicalHistoryDto ToDto(PatientMedicalHistory h) =>
        new(h.Id, h.Category, h.Description, h.OnsetDate, h.IsEnteredInError, h.CreatedAt, h.RowVersion);
}

/// <summary>Patient documents: PDF/JPEG/PNG up to 10 MB, type checked from content; removal is a soft delete.</summary>
public sealed class PatientDocumentManager(
    IPatientRepository patients,
    IPatientDocumentRepository documents,
    IPatientUnitOfWork unitOfWork,
    IFileStorageService fileStorage,
    IDateTimeProvider dateTime,
    ICurrentUser currentUser) : IPatientDocumentManager
{
    public const long MaxDocumentBytes = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<PatientDocumentDto>> GetAsync(Guid patientId, CancellationToken cancellationToken)
    {
        await GetPatientAsync(patientId, cancellationToken);
        return (await documents.GetForPatientAsync(patientId, cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<PatientDocumentDto> UploadAsync(
        Guid patientId, DocumentCategory category, string title, string fileName, Stream content, long length,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(category))
        {
            throw new BusinessException(PatientErrorCodes.DocumentInvalid, "Select a document category.", "category");
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 150)
        {
            throw new BusinessException(PatientErrorCodes.DocumentInvalid, "Enter a title of up to 150 characters.", "title");
        }

        if (length <= 0 || length > MaxDocumentBytes)
        {
            throw new BusinessException(PatientErrorCodes.DocumentInvalid, "The document must be at most 10 MB.", "file");
        }

        var patient = await GetPatientAsync(patientId, cancellationToken);
        patient.EnsureEditable();

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        // PDF, JPEG or PNG only (WebP is accepted for photos but not as a clinical document).
        var type = FileSignatures.DetectPdf(bytes)
            ?? (FileSignatures.DetectImage(bytes) is { Extension: "png" or "jpg" } image ? image : null);
        if (type is null)
        {
            throw new BusinessException(PatientErrorCodes.DocumentInvalid, "Documents must be PDF, JPEG or PNG files.", "file");
        }

        buffer.Position = 0;
        var stored = await fileStorage.SaveAsync(buffer, $"patients/{patientId:N}/documents", type.Extension, type.ContentType, cancellationToken);
        var document = PatientDocument.Create(patientId, category, title, SafeFileName(fileName, type.Extension),
            stored.StoragePath, stored.ContentType, stored.SizeBytes, stored.Sha256);

        await documents.AddAsync(document, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(stored.StoragePath, CancellationToken.None);
            throw;
        }

        return ToDto(document);
    }

    public async Task<DocumentContent> OpenAsync(Guid patientId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync(patientId, documentId, cancellationToken);
        var stream = await fileStorage.OpenReadAsync(document.StoragePath, cancellationToken)
            ?? throw new NotFoundException("Document file", documentId);
        return new DocumentContent(stream, document.ContentType, document.FileName);
    }

    public async Task RemoveAsync(Guid patientId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync(patientId, documentId, cancellationToken);
        document.Remove(dateTime.UtcNow, currentUser.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<PatientEntity> GetPatientAsync(Guid id, CancellationToken cancellationToken) =>
        await patients.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Patient", id);

    private async Task<PatientDocument> GetDocumentAsync(Guid patientId, Guid id, CancellationToken cancellationToken) =>
        await documents.GetByIdAsync(id, cancellationToken) is { IsDeleted: false } d && d.PatientId == patientId
            ? d
            : throw new NotFoundException("Document", id);

    /// <summary>Keeps a readable name for downloads; strips paths and unsafe characters.</summary>
    private static string SafeFileName(string fileName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName ?? string.Empty));
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.').ToArray()).Trim();
        if (name.Length == 0)
        {
            name = "document";
        }

        return $"{(name.Length > 100 ? name[..100] : name)}.{extension}";
    }

    private static PatientDocumentDto ToDto(PatientDocument d) =>
        new(d.Id, d.Category, d.Title, d.FileName, d.ContentType, d.SizeBytes, d.CreatedAt, d.CreatedBy);
}

/// <summary>
/// Duplicate merge workflow (Q-PAT-10): a request names the duplicate (source) and the surviving (target) record;
/// on approval the source becomes read-only "Merged" and points to the target. Nothing is deleted.
/// </summary>
public sealed class PatientMergeManager(
    IPatientRepository patients,
    IPatientMergeRequestRepository requests,
    IPatientUnitOfWork unitOfWork,
    IDateTimeProvider dateTime,
    ICurrentUser currentUser) : IPatientMergeManager
{
    public async Task<PagedResponse<MergeRequestDto>> SearchAsync(MergeRequestQuery query, CancellationToken cancellationToken)
    {
        var page = await requests.SearchAsync(query, cancellationToken);
        var ids = page.Items.SelectMany(r => new[] { r.SourcePatientId, r.TargetPatientId }).Distinct().ToList();
        var byId = (await patients.GetByIdsAsync(ids, cancellationToken)).ToDictionary(p => p.Id);
        return page.Map(r => ToDto(r, byId));
    }

    public async Task<MergeRequestDto> RequestAsync(MergeRequestCreateDto request, CancellationToken cancellationToken)
    {
        var source = await GetPatientAsync(request.SourcePatientId, cancellationToken);
        var target = await GetPatientAsync(request.TargetPatientId, cancellationToken);
        EnsureMergeable(source, target);

        if (await requests.HasPendingForSourceAsync(source.Id, cancellationToken))
        {
            throw new ConflictException("A merge request for this patient is already waiting for review.", PatientErrorCodes.MergePending);
        }

        var mergeRequest = PatientMergeRequest.Create(source.Id, target.Id, request.Reason);
        await requests.AddAsync(mergeRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(mergeRequest, new() { [source.Id] = source, [target.Id] = target });
    }

    public async Task<MergeRequestDto> ApproveAsync(Guid id, MergeReviewDto request, CancellationToken cancellationToken)
    {
        var mergeRequest = await GetRequestAsync(id, cancellationToken);

        // Fails first if the request was already reviewed; nothing is saved unless every check passes.
        mergeRequest.Approve(currentUser.UserId, dateTime.UtcNow, request.Note);

        var source = await GetPatientAsync(mergeRequest.SourcePatientId, cancellationToken);
        var target = await GetPatientAsync(mergeRequest.TargetPatientId, cancellationToken);
        EnsureMergeable(source, target);

        // Request approval and the patient status change are saved together (one context, one transaction).
        source.MarkMergedInto(target.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // When Appointment/OPD/IPD/Billing exist, they will re-point their records via a PatientMerged event.
        return ToDto(mergeRequest, new() { [source.Id] = source, [target.Id] = target });
    }

    public async Task<MergeRequestDto> RejectAsync(Guid id, MergeReviewDto request, CancellationToken cancellationToken)
    {
        var mergeRequest = await GetRequestAsync(id, cancellationToken);
        mergeRequest.Reject(currentUser.UserId, dateTime.UtcNow, request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var byId = (await patients.GetByIdsAsync([mergeRequest.SourcePatientId, mergeRequest.TargetPatientId], cancellationToken))
            .ToDictionary(p => p.Id);
        return ToDto(mergeRequest, byId);
    }

    private static void EnsureMergeable(PatientEntity source, PatientEntity target)
    {
        if (source.Status == PatientStatus.Merged || target.Status == PatientStatus.Merged)
        {
            throw new BusinessException(PatientErrorCodes.MergeInvalid, "One of these records has already been merged.");
        }

        if (target.Status != PatientStatus.Active)
        {
            throw new BusinessException(PatientErrorCodes.MergeInvalid, "The surviving record must be active.");
        }
    }

    private async Task<PatientEntity> GetPatientAsync(Guid id, CancellationToken cancellationToken) =>
        await patients.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Patient", id);

    private async Task<PatientMergeRequest> GetRequestAsync(Guid id, CancellationToken cancellationToken) =>
        await requests.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Merge request", id);

    private static MergeRequestDto ToDto(PatientMergeRequest r, Dictionary<Guid, PatientEntity> patients)
    {
        var source = patients.GetValueOrDefault(r.SourcePatientId);
        var target = patients.GetValueOrDefault(r.TargetPatientId);
        return new MergeRequestDto(
            r.Id, r.SourcePatientId, source?.MRNumber ?? "?", source?.FullName ?? "?",
            r.TargetPatientId, target?.MRNumber ?? "?", target?.FullName ?? "?",
            r.Reason, r.Status, r.CreatedAt, r.CreatedBy, r.ReviewedAt, r.ReviewNote);
    }
}
