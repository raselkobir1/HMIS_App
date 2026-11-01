using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Patient.Domain.Entities;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Application;

public interface IPatientUnitOfWork : IUnitOfWork;

public interface IPatientRepository : IRepository<PatientEntity>, IConcurrencyRepository<PatientEntity>
{
    Task<PatientEntity?> GetByMRNumberAsync(string mrNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Patients registered since the instant, and all patients with status Active.</summary>
    Task<(int RegisteredSince, int Active)> CountAsync(DateTime sinceUtc, CancellationToken cancellationToken);

    /// <summary>Untracked: records merged directly into any of the given patients.</summary>
    Task<IReadOnlyList<PatientEntity>> GetMergedIntoAsync(IReadOnlyCollection<Guid> survivorIds, CancellationToken cancellationToken);

    Task<bool> IdentificationExistsAsync(IdentificationType type, string normalizedNumber, Guid? excludePatientId, CancellationToken cancellationToken);

    /// <summary>Single search box: MR number (prefix), mobile, name (fuzzy, indexed) or exact identification number.</summary>
    Task<PagedResponse<PatientEntity>> SearchAsync(PatientSearchQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<DuplicateCandidateRow>> FindDuplicateCandidatesAsync(DuplicateCheckQuery query, CancellationToken cancellationToken);
}

/// <summary>Raw duplicate-match row; the manager turns match flags into user-facing reasons.</summary>
public sealed record DuplicateCandidateRow(
    Guid Id,
    string MRNumber,
    string FullName,
    Gender Gender,
    DateOnly DateOfBirth,
    string MobileNumber,
    bool SameIdentification,
    bool SameMobileSimilarName,
    bool SameBirthSimilarName,
    double NameSimilarity);

public interface IPatientAllergyRepository : IRepository<PatientAllergy>, IConcurrencyRepository<PatientAllergy>
{
    Task<IReadOnlyList<PatientAllergy>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken);
}

public interface IPatientMedicalHistoryRepository : IRepository<PatientMedicalHistory>, IConcurrencyRepository<PatientMedicalHistory>
{
    Task<IReadOnlyList<PatientMedicalHistory>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken);
}

public interface IPatientDocumentRepository : IRepository<PatientDocument>
{
    Task<IReadOnlyList<PatientDocument>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken);
}

public interface IPatientMergeRequestRepository : IRepository<PatientMergeRequest>
{
    Task<bool> HasPendingForSourceAsync(Guid sourcePatientId, CancellationToken cancellationToken);

    Task<PagedResponse<PatientMergeRequest>> SearchAsync(MergeRequestQuery query, CancellationToken cancellationToken);
}

public interface IPatientManager
{
    Task<PatientCountsDto> GetCountsAsync(CancellationToken cancellationToken);

    Task<PagedResponse<PatientListItemDto>> SearchAsync(PatientSearchQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<DuplicateCandidateDto>> FindDuplicatesAsync(DuplicateCheckQuery query, CancellationToken cancellationToken);

    Task<PatientDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PatientDto> GetByMRNumberAsync(string mrNumber, CancellationToken cancellationToken);

    Task<PatientDto> RegisterAsync(PatientRegisterDto request, CancellationToken cancellationToken);

    Task<PatientDto> UpdateAsync(Guid id, PatientUpdateDto request, CancellationToken cancellationToken);

    Task<PatientDto> SetStatusAsync(Guid id, PatientStatusUpdateDto request, CancellationToken cancellationToken);

    Task<PatientDto> UploadPhotoAsync(Guid id, Stream content, long length, CancellationToken cancellationToken);

    Task<PatientDto> RemovePhotoAsync(Guid id, CancellationToken cancellationToken);

    Task<DocumentContent?> OpenPhotoAsync(Guid id, CancellationToken cancellationToken);
}

public interface IPatientMedicalManager
{
    Task<MedicalInfoDto> GetAsync(Guid patientId, CancellationToken cancellationToken);

    Task<AllergyDto> AddAllergyAsync(Guid patientId, AllergyRequestDto request, CancellationToken cancellationToken);

    Task<AllergyDto> UpdateAllergyAsync(Guid patientId, Guid allergyId, AllergyRequestDto request, CancellationToken cancellationToken);

    Task<AllergyDto> SetAllergyStatusAsync(Guid patientId, Guid allergyId, AllergyStatusDto request, CancellationToken cancellationToken);

    Task<MedicalHistoryDto> AddHistoryAsync(Guid patientId, MedicalHistoryRequestDto request, CancellationToken cancellationToken);

    Task<MedicalHistoryDto> UpdateHistoryAsync(Guid patientId, Guid historyId, MedicalHistoryRequestDto request, CancellationToken cancellationToken);

    Task<MedicalHistoryDto> MarkHistoryEnteredInErrorAsync(Guid patientId, Guid historyId, CancellationToken cancellationToken);
}

public interface IPatientDocumentManager
{
    Task<IReadOnlyList<PatientDocumentDto>> GetAsync(Guid patientId, CancellationToken cancellationToken);

    Task<PatientDocumentDto> UploadAsync(
        Guid patientId, DocumentCategory category, string title, string fileName, Stream content, long length, CancellationToken cancellationToken);

    Task<DocumentContent> OpenAsync(Guid patientId, Guid documentId, CancellationToken cancellationToken);

    Task RemoveAsync(Guid patientId, Guid documentId, CancellationToken cancellationToken);
}

public interface IPatientMergeManager
{
    Task<PagedResponse<MergeRequestDto>> SearchAsync(MergeRequestQuery query, CancellationToken cancellationToken);

    Task<MergeRequestDto> RequestAsync(MergeRequestCreateDto request, CancellationToken cancellationToken);

    Task<MergeRequestDto> ApproveAsync(Guid id, MergeReviewDto request, CancellationToken cancellationToken);

    Task<MergeRequestDto> RejectAsync(Guid id, MergeReviewDto request, CancellationToken cancellationToken);
}
