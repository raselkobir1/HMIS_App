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
