using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.Modules.Patient.Domain.Entities;

namespace Hmis.Modules.Patient.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class PatientApplicationMarker;

public static class PatientPermissions
{
    public const string Module = "Patient";

    public const string View = "Patient.View";
    public const string Create = "Patient.Create";
    public const string Update = "Patient.Update";
    public const string Deactivate = "Patient.Deactivate";
    public const string MergeRequest = "Patient.Merge.Request";
    public const string MergeApprove = "Patient.Merge.Approve";
    public const string MedicalInfoView = "Patient.MedicalInfo.View";
    public const string MedicalInfoManage = "Patient.MedicalInfo.Manage";
    public const string DocumentsView = "Patient.Documents.View";
    public const string DocumentsUpload = "Patient.Documents.Upload";
    public const string DocumentsDelete = "Patient.Documents.Delete";
}

/// <summary>Defaults from docs/ROLE-PERMISSION-MATRIX.md §2. Clinical permissions require an explicit grant (Q-SEC-1).</summary>
public sealed class PatientPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        // The cash counter finds patients to bill and print receipts (Billing, Phase 3).
        new(PatientPermissions.View, PatientPermissions.Module, "Search and view patient demographics",
            [SystemRoles.HospitalAdministrator, SystemRoles.Receptionist, SystemRoles.Doctor, SystemRoles.Nurse, SystemRoles.Accountant]),
        new(PatientPermissions.Create, PatientPermissions.Module, "Register patients", [SystemRoles.Receptionist]),
        new(PatientPermissions.Update, PatientPermissions.Module, "Update patient demographics and photo", [SystemRoles.Receptionist]),
        new(PatientPermissions.Deactivate, PatientPermissions.Module, "Deactivate or reactivate patient records", [SystemRoles.HospitalAdministrator]),
        new(PatientPermissions.MergeRequest, PatientPermissions.Module, "Request merging duplicate patient records", [SystemRoles.Receptionist]),
        new(PatientPermissions.MergeApprove, PatientPermissions.Module, "Approve or reject patient merge requests", [SystemRoles.HospitalAdministrator]),
        new(PatientPermissions.MedicalInfoView, PatientPermissions.Module, "View allergies and medical history",
            [SystemRoles.Doctor, SystemRoles.Nurse], RequiresExplicitGrant: true),
        new(PatientPermissions.MedicalInfoManage, PatientPermissions.Module, "Record allergies and medical history",
            [SystemRoles.Doctor, SystemRoles.Nurse], RequiresExplicitGrant: true),
        new(PatientPermissions.DocumentsView, PatientPermissions.Module, "View patient documents",
            [SystemRoles.Receptionist, SystemRoles.Doctor, SystemRoles.Nurse]),
        new(PatientPermissions.DocumentsUpload, PatientPermissions.Module, "Upload patient documents",
            [SystemRoles.Receptionist, SystemRoles.Doctor, SystemRoles.Nurse]),
        new(PatientPermissions.DocumentsDelete, PatientPermissions.Module, "Remove patient documents", [])
    ];
}

public static class PatientErrorCodes
{
    public const string IdentificationDuplicate = "PATIENT_IDENTIFICATION_DUPLICATE";
    public const string PhotoInvalid = "PATIENT_PHOTO_INVALID";
    public const string DocumentInvalid = "PATIENT_DOCUMENT_INVALID";
    public const string MergePending = "PATIENT_MERGE_ALREADY_PENDING";
    public const string MergeInvalid = "PATIENT_MERGE_INVALID";
    public const string MergeBlocked = "PATIENT_MERGE_BLOCKED";
}

// ---------- Patient DTOs ----------

public sealed record AddressDto
{
    public string? Line { get; init; }
    public string? Upazila { get; init; }
    public string? District { get; init; }
    public string? PostCode { get; init; }
}

public sealed record EmergencyContactDto
{
    public string Name { get; init; } = string.Empty;
    public string Relation { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
}

/// <summary>Demographics shared by registration and update.</summary>
public record PatientDetailsDto
{
    public string FullName { get; init; } = string.Empty;
    public string? FullNameBangla { get; init; }
    public Gender Gender { get; init; }
    public DateOnly DateOfBirth { get; init; }
    public bool IsDateOfBirthEstimated { get; init; }
    public BloodGroup? BloodGroup { get; init; }
    public string MobileNumber { get; init; } = string.Empty;
    public string? Email { get; init; }
    public AddressDto PresentAddress { get; init; } = new();
    public AddressDto PermanentAddress { get; init; } = new();
    public IdentificationType? IdentificationType { get; init; }
    public string? IdentificationNumber { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianRelation { get; init; }
    public IReadOnlyList<EmergencyContactDto> EmergencyContacts { get; init; } = [];
}

public sealed record PatientRegisterDto : PatientDetailsDto;

public sealed record PatientUpdateDto : PatientDetailsDto
{
    public uint RowVersion { get; init; }
}

public sealed record PatientStatusUpdateDto
{
    public bool IsActive { get; init; }
}

public sealed record PatientDto(
    Guid Id,
    string MRNumber,
    string FullName,
    string? FullNameBangla,
    Gender Gender,
    DateOnly DateOfBirth,
    bool IsDateOfBirthEstimated,
    BloodGroup? BloodGroup,
    string MobileNumber,
    string? Email,
    AddressDto PresentAddress,
    AddressDto PermanentAddress,
    IdentificationType? IdentificationType,
    string? IdentificationNumber,
    string? GuardianName,
    string? GuardianRelation,
    IReadOnlyList<EmergencyContactDto> EmergencyContacts,
    bool HasPhoto,
    PatientStatus Status,
    Guid? MergedIntoPatientId,
    string? MergedIntoMRNumber,
    DateTime? DeceasedAt,
    // Duplicate records merged into this patient (their history is shown with this record).
    IReadOnlyList<MergedRecordDto> MergedRecords,
    DateTime CreatedAt,
    uint RowVersion)
{
    /// <summary>Set only in the registration response when a registration fee was billed.</summary>
    public RegistrationBillDto? RegistrationBill { get; init; }
}

public sealed record MergedRecordDto(Guid Id, string MRNumber);

/// <summary>Dashboard counts.</summary>
public sealed record PatientCountsDto(int RegisteredToday, int ActivePatients);

public sealed record RegistrationBillDto(Guid InvoiceId, string InvoiceNumber, decimal Amount);

public sealed record PatientListItemDto(
    Guid Id,
    string MRNumber,
    string FullName,
    Gender Gender,
    DateOnly DateOfBirth,
    bool IsDateOfBirthEstimated,
    string MobileNumber,
    string? District,
    PatientStatus Status);

public sealed class PatientSearchQuery : PaginationRequest
{
    public PatientStatus? Status { get; set; }
}

public sealed record DuplicateCheckQuery
{
    public string? FullName { get; set; }
    public string? MobileNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public IdentificationType? IdentificationType { get; set; }
    public string? IdentificationNumber { get; set; }

    /// <summary>When editing, the patient being edited is not reported as its own duplicate.</summary>
    public Guid? ExcludePatientId { get; set; }
}

public sealed record DuplicateCandidateDto(
    Guid Id,
    string MRNumber,
    string FullName,
    Gender Gender,
    DateOnly DateOfBirth,
    string MobileNumber,
    IReadOnlyList<string> Reasons,
    bool IsBlocking);

// ---------- Medical info DTOs ----------

public sealed record AllergyDto(
    Guid Id, string Allergen, AllergyType Type, AllergySeverity Severity, string? Reaction, AllergyStatus Status,
    DateTime CreatedAt, uint RowVersion);

public sealed record MedicalHistoryDto(
    Guid Id, MedicalHistoryCategory Category, string Description, DateOnly? OnsetDate, bool IsEnteredInError,
    DateTime CreatedAt, uint RowVersion);

public sealed record MedicalInfoDto(IReadOnlyList<AllergyDto> Allergies, IReadOnlyList<MedicalHistoryDto> History);

public sealed record AllergyRequestDto
{
    public string Allergen { get; init; } = string.Empty;
    public AllergyType Type { get; init; }
    public AllergySeverity Severity { get; init; }
    public string? Reaction { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record AllergyStatusDto
{
    public AllergyStatus Status { get; init; }
}

public sealed record MedicalHistoryRequestDto
{
    public MedicalHistoryCategory Category { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateOnly? OnsetDate { get; init; }
    public uint RowVersion { get; init; }
}

// ---------- Documents & merge DTOs ----------

public sealed record PatientDocumentDto(
    Guid Id, DocumentCategory Category, string Title, string FileName, string ContentType, long SizeBytes,
    DateTime UploadedAt, Guid? UploadedBy);

public sealed record DocumentContent(Stream Content, string ContentType, string FileName);

public sealed record MergeRequestCreateDto
{
    public Guid SourcePatientId { get; init; }
    public Guid TargetPatientId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record MergeReviewDto
{
    public string? Note { get; init; }
}

public sealed class MergeRequestQuery : PaginationRequest
{
    public MergeRequestStatus? Status { get; set; }
}

public sealed record MergeRequestDto(
    Guid Id,
    Guid SourcePatientId,
    string SourceMRNumber,
    string SourceName,
    Guid TargetPatientId,
    string TargetMRNumber,
    string TargetName,
    string Reason,
    MergeRequestStatus Status,
    DateTime RequestedAt,
    Guid? RequestedBy,
    DateTime? ReviewedAt,
    string? ReviewNote);
