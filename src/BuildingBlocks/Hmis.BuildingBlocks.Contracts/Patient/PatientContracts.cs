namespace Hmis.BuildingBlocks.Contracts.Patient;

// Contracts owned and implemented by the Patient module, for Appointment, OPD, IPD, Billing and others.

public sealed record PatientSummary(
    Guid Id,
    string MRNumber,
    string FullName,
    string Gender,
    DateOnly? DateOfBirth,
    string MobileNumber,
    string Status,
    Guid? MergedIntoPatientId);

public interface IPatientLookup
{
    Task<PatientSummary?> GetAsync(Guid patientId, CancellationToken cancellationToken);

    Task<PatientSummary?> GetByMRNumberAsync(string mrNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientSummary>> GetManyAsync(IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken);

    /// <summary>
    /// The patient and every duplicate record merged into it (directly or through earlier merges), so history screens can
    /// show the records of merged duplicates with the surviving patient.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetWithMergedAsync(Guid patientId, CancellationToken cancellationToken);
}

/// <summary>
/// Implemented by modules that hold open, patient-linked work (Appointment, IPD, Billing). A duplicate record cannot be merged
/// while any guard reports a blocker; staff resolve it first (Q-PAT-14).
/// </summary>
public interface IPatientMergeGuard
{
    /// <summary>Human-readable reasons, e.g. "Admitted (ADM26000012, General Ward bed G-1)"; empty when nothing blocks.</summary>
    Task<IReadOnlyList<string>> GetMergeBlockersAsync(Guid patientId, CancellationToken cancellationToken);
}

/// <summary>Patient status changes requested by other modules.</summary>
public interface IPatientStatusService
{
    /// <summary>Records the patient's death (e.g. an IPD discharge of type Death). No-op when already recorded.</summary>
    Task MarkDeceasedAsync(Guid patientId, DateTime diedAt, string source, CancellationToken cancellationToken);
}

/// <summary>Active allergy shown as a safety alert in clinical screens.</summary>
public sealed record PatientAllergyAlert(string Allergen, string Type, string Severity, string? Reaction);

/// <summary>Past illnesses, surgeries and current medications recorded on the patient profile.</summary>
public sealed record PatientHistoryItem(string Category, string Description, DateOnly? OnsetDate);

public sealed record PatientClinicalSummary(
    IReadOnlyList<PatientAllergyAlert> Allergies,
    IReadOnlyList<PatientHistoryItem> History);

/// <summary>
/// Baseline clinical data for consultations. Excludes entries marked as entered in error and resolved allergies.
/// The caller is responsible for auditing access (BRD §11).
/// </summary>
public interface IPatientClinicalSummary
{
    Task<PatientClinicalSummary> GetAsync(Guid patientId, CancellationToken cancellationToken);
}
