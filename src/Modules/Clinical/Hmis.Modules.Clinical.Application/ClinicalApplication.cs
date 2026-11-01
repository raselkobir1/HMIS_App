using Hmis.BuildingBlocks.Application.Security;
using Hmis.Modules.Clinical.Domain.Entities;

namespace Hmis.Modules.Clinical.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class ClinicalApplicationMarker;

public static class ClinicalPermissions
{
    public const string Module = "Clinical";

    public const string View = "Clinical.View";
    public const string VitalsRecord = "Clinical.Vitals.Record";
    public const string ConsultationManage = "Clinical.Consultation.Manage";
    public const string InpatientWrite = "Clinical.Inpatient.Write";
}

/// <summary>Clinical permissions require an explicit grant: administrators do not receive them automatically (BRD §11).</summary>
public sealed class ClinicalPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(ClinicalPermissions.View, ClinicalPermissions.Module, "View consultations, prescriptions and vitals",
            [SystemRoles.Doctor, SystemRoles.Nurse], RequiresExplicitGrant: true),
        new(ClinicalPermissions.VitalsRecord, ClinicalPermissions.Module, "Record vital signs",
            [SystemRoles.Doctor, SystemRoles.Nurse], RequiresExplicitGrant: true),
        new(ClinicalPermissions.ConsultationManage, ClinicalPermissions.Module, "Write, finalize and amend own consultations and prescriptions",
            [SystemRoles.Doctor], RequiresExplicitGrant: true),
        new(ClinicalPermissions.InpatientWrite, ClinicalPermissions.Module, "Write inpatient progress notes and medication orders",
            [SystemRoles.Doctor], RequiresExplicitGrant: true)
    ];
}

public static class ClinicalErrorCodes
{
    public const string EncounterNotFound = "ENCOUNTER_NOT_FOUND";
    public const string EncounterNotActive = "ENCOUNTER_NOT_ACTIVE";
    public const string NotYourConsultation = "CONSULTATION_NOT_YOURS";
    public const string NotLinkedToDoctor = "CLINICAL_NOT_A_DOCTOR";
    public const string StayNotActive = "IPD_STAY_NOT_ACTIVE";
    public const string MedicationStart = "MEDICATION_START";
}

// ---------- Vitals ----------

public sealed record VitalSignRequestDto
{
    public int? Systolic { get; init; }
    public int? Diastolic { get; init; }
    public int? Pulse { get; init; }
    public decimal? TemperatureF { get; init; }
    public int? RespiratoryRate { get; init; }
    public int? SpO2 { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? HeightCm { get; init; }
    public string? Notes { get; init; }
}

public sealed record EnteredInErrorDto
{
    public string Reason { get; init; } = string.Empty;
}

public sealed record VitalSignDto(
    Guid Id, DateTime RecordedAt, string? RecordedBy, int? Systolic, int? Diastolic, int? Pulse, decimal? TemperatureF,
    int? RespiratoryRate, int? SpO2, decimal? WeightKg, decimal? HeightCm, decimal? Bmi, string? Notes,
    bool IsEnteredInError, string? ErrorReason);

// ---------- Consultation ----------

public sealed record DiagnosisDto(Guid? Id, DiagnosisType Type, string Description);

public sealed record PrescriptionItemDto(
    Guid? Id, string MedicineName, string? Form, string? Strength, string? Route, string DosePattern, MedicineTiming Timing,
    int? DurationValue, DurationUnit DurationUnit, int? Quantity, string? Instructions);

public sealed record InvestigationDto(Guid? Id, string Name, string? Notes);

public sealed record AddendumDto(Guid Id, string Text, DateTime CreatedAt, string? CreatedBy);

public sealed record ConsultationDto(
    Guid Id,
    EncounterType EncounterType,
    Guid EncounterId,
    string EncounterNumber,
    DateOnly EncounterDate,
    Guid PatientId,
    Guid DoctorId,
    ConsultationStatus Status,
    string? ChiefComplaint,
    string? HistoryOfPresentIllness,
    string? Examination,
    string? Advice,
    DateOnly? FollowUpDate,
    string? FollowUpInstructions,
    string? ReferralTo,
    string? ReferralReason,
    IReadOnlyList<DiagnosisDto> Diagnoses,
    IReadOnlyList<PrescriptionItemDto> Prescription,
    IReadOnlyList<InvestigationDto> Investigations,
    IReadOnlyList<AddendumDto> Addenda,
    DateTime? FinalizedAt,
    uint RowVersion);

/// <summary>Saves the whole draft. Lines with an id are updated, new lines have no id, missing lines are removed.</summary>
public sealed record ConsultationSaveDto
{
    public string? ChiefComplaint { get; init; }
    public string? HistoryOfPresentIllness { get; init; }
    public string? Examination { get; init; }
    public string? Advice { get; init; }
    public DateOnly? FollowUpDate { get; init; }
    public string? FollowUpInstructions { get; init; }
    public string? ReferralTo { get; init; }
    public string? ReferralReason { get; init; }
    public IReadOnlyList<DiagnosisDto> Diagnoses { get; init; } = [];
    public IReadOnlyList<PrescriptionItemDto> Prescription { get; init; } = [];
    public IReadOnlyList<InvestigationDto> Investigations { get; init; } = [];
    public uint RowVersion { get; init; }
}

public sealed record FinalizeDto
{
    public uint RowVersion { get; init; }
}

public sealed record AddendumCreateDto
{
    public string Text { get; init; } = string.Empty;
}

// ---------- Context shown with a consultation ----------

public sealed record PatientInfoDto(Guid Id, string MRNumber, string FullName, string Gender, DateOnly? DateOfBirth, string MobileNumber);

public sealed record DoctorInfoDto(Guid Id, string FullName, string Specialization, string Qualifications, string RegistrationNumber);

public sealed record EncounterInfoDto(Guid Id, string Number, DateOnly Date, int SerialNumber, string VisitType, string Status);

public sealed record AllergyAlertDto(string Allergen, string Type, string Severity, string? Reaction);

public sealed record HistoryItemDto(string Category, string Description, DateOnly? OnsetDate);

public sealed record ConsultationSummaryDto(
    Guid Id, EncounterType EncounterType, string EncounterNumber, DateOnly EncounterDate, Guid DoctorId, string? DoctorName,
    ConsultationStatus Status, string? PrimaryDiagnosis, int MedicineCount);

/// <summary>Everything the consultation screen needs for one OPD encounter.</summary>
public sealed record OpdWorkspaceDto(
    EncounterInfoDto Encounter,
    PatientInfoDto Patient,
    DoctorInfoDto? Doctor,
    IReadOnlyList<AllergyAlertDto> Allergies,
    IReadOnlyList<HistoryItemDto> History,
    IReadOnlyList<VitalSignDto> Vitals,
    ConsultationDto? Consultation,
    IReadOnlyList<ConsultationSummaryDto> PreviousConsultations,
    bool CanWrite);

/// <summary>A consultation with the details needed to view or print the prescription.</summary>
public sealed record ConsultationRecordDto(
    ConsultationDto Consultation,
    PatientInfoDto Patient,
    DoctorInfoDto? Doctor,
    IReadOnlyList<AllergyAlertDto> Allergies,
    VitalSignDto? LatestVitals,
    bool CanWrite);
