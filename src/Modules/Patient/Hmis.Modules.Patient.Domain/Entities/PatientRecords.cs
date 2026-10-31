using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Patient.Domain.Entities;

public enum AllergyType
{
    Drug,
    Food,
    Environmental,
    Other
}

public enum AllergySeverity
{
    Mild,
    Moderate,
    Severe
}

public enum AllergyStatus
{
    Active,
    Resolved,

    /// <summary>Recorded by mistake. Kept for the record (never deleted) but excluded from clinical warnings.</summary>
    EnteredInError
}

/// <summary>Allergy entry. Never deleted: corrections are made by status so the clinical history is preserved.</summary>
[Audited]
public sealed class PatientAllergy : AuditableEntity, IHasConcurrencyToken
{
    private PatientAllergy() { }

    public Guid PatientId { get; private set; }
    public string Allergen { get; private set; } = string.Empty;
    public AllergyType Type { get; private set; }
    public AllergySeverity Severity { get; private set; }
    public string? Reaction { get; private set; }
    public AllergyStatus Status { get; private set; } = AllergyStatus.Active;
    public uint RowVersion { get; private set; }

    public static PatientAllergy Record(Guid patientId, string allergen, AllergyType type, AllergySeverity severity, string? reaction)
    {
        var allergy = new PatientAllergy { PatientId = patientId };
        allergy.Update(allergen, type, severity, reaction);
        return allergy;
    }

    public void Update(string allergen, AllergyType type, AllergySeverity severity, string? reaction)
    {
        EnsureNotInError();
        Allergen = allergen.Trim();
        Type = type;
        Severity = severity;
        Reaction = string.IsNullOrWhiteSpace(reaction) ? null : reaction.Trim();
    }

    public void ChangeStatus(AllergyStatus status)
    {
        EnsureNotInError();
        Status = status;
    }

    private void EnsureNotInError()
    {
        if (Status == AllergyStatus.EnteredInError)
        {
            throw new DomainException("ALLERGY_ENTERED_IN_ERROR", "This allergy was marked as entered in error and can no longer be changed.");
        }
    }
}

public enum MedicalHistoryCategory
{
    PastIllness,
    Surgical,
    Family,
    Social,
    CurrentMedication
}

/// <summary>Baseline medical history entry. Corrections are audited; mistakes are marked, never deleted.</summary>
[Audited]
public sealed class PatientMedicalHistory : AuditableEntity, IHasConcurrencyToken
{
    private PatientMedicalHistory() { }

    public Guid PatientId { get; private set; }
    public MedicalHistoryCategory Category { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateOnly? OnsetDate { get; private set; }
    public bool IsEnteredInError { get; private set; }
    public uint RowVersion { get; private set; }

    public static PatientMedicalHistory Record(Guid patientId, MedicalHistoryCategory category, string description, DateOnly? onsetDate)
    {
        var entry = new PatientMedicalHistory { PatientId = patientId };
        entry.Update(category, description, onsetDate);
        return entry;
    }

    public void Update(MedicalHistoryCategory category, string description, DateOnly? onsetDate)
    {
        if (IsEnteredInError)
        {
            throw new DomainException("HISTORY_ENTERED_IN_ERROR", "This entry was marked as entered in error and can no longer be changed.");
        }

        Category = category;
        Description = description.Trim();
        OnsetDate = onsetDate;
    }

    public void MarkEnteredInError() => IsEnteredInError = true;
}

public enum DocumentCategory
{
    LabReport,
    ImagingReport,
    Prescription,
    DischargeSummary,
    Identification,
    Consent,
    Other
}

/// <summary>Uploaded patient document. Removal hides the document (soft delete) and keeps the file and the audit trail.</summary>
[Audited]
public sealed class PatientDocument : AuditableEntity
{
    private PatientDocument() { }

    public Guid PatientId { get; private set; }
    public DocumentCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public static PatientDocument Create(
        Guid patientId, DocumentCategory category, string title, string fileName,
        string storagePath, string contentType, long sizeBytes, string sha256) =>
        new()
        {
            PatientId = patientId,
            Category = category,
            Title = title.Trim(),
            FileName = fileName,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256
        };

    public void Remove(DateTime utcNow, Guid? userId)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = utcNow;
        DeletedBy = userId;
    }
}

public enum MergeRequestStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>Request to merge a duplicate (source) patient into the surviving (target) record; needs approval.</summary>
[Audited]
public sealed class PatientMergeRequest : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private PatientMergeRequest() { }

    public Guid SourcePatientId { get; private set; }
    public Guid TargetPatientId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public MergeRequestStatus Status { get; private set; } = MergeRequestStatus.Pending;
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }
    public uint RowVersion { get; private set; }

    public static PatientMergeRequest Create(Guid sourcePatientId, Guid targetPatientId, string reason)
    {
        if (sourcePatientId == targetPatientId)
        {
            throw new DomainException("PATIENT_MERGE_SELF", "A patient cannot be merged into itself.");
        }

        return new PatientMergeRequest { SourcePatientId = sourcePatientId, TargetPatientId = targetPatientId, Reason = reason.Trim() };
    }

    public void Approve(Guid? reviewerId, DateTime utcNow, string? note) => Review(MergeRequestStatus.Approved, reviewerId, utcNow, note);

    public void Reject(Guid? reviewerId, DateTime utcNow, string? note) => Review(MergeRequestStatus.Rejected, reviewerId, utcNow, note);

    private void Review(MergeRequestStatus status, Guid? reviewerId, DateTime utcNow, string? note)
    {
        if (Status != MergeRequestStatus.Pending)
        {
            throw new DomainException("MERGE_REQUEST_CLOSED", "This merge request has already been reviewed.");
        }

        Status = status;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
