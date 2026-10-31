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
