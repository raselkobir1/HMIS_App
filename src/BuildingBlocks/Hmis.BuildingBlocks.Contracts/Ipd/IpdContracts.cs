namespace Hmis.BuildingBlocks.Contracts.Ipd;

// Contracts owned and implemented by the IPD module.

/// <summary>
/// An inpatient stay as seen by Clinical and Nursing. Status is the IPD admission status
/// (Admitted, DischargeOrdered, FinalBilled, Discharged); IsActive is false once the patient has left the bed.
/// </summary>
public sealed record InpatientStay(
    Guid AdmissionId,
    string AdmissionNumber,
    Guid PatientId,
    Guid AttendingDoctorId,
    string Status,
    bool IsActive,
    DateTime AdmittedAt,
    DateTime? ReleasedAt,
    Guid? WardId,
    string? WardName,
    string? BedNumber);

public sealed record WardSummary(Guid Id, string Code, string Name, bool IsActive);

public interface IInpatientLookup
{
    Task<InpatientStay?> GetAsync(Guid admissionId, CancellationToken cancellationToken);

    /// <summary>Patients still in a bed, optionally in one ward, ordered by ward and bed.</summary>
    Task<IReadOnlyList<InpatientStay>> GetActiveAsync(Guid? wardId, CancellationToken cancellationToken);

    /// <summary>True when the doctor is (or was) the attending doctor of one of the patient's admissions.</summary>
    Task<bool> IsAttendingDoctorAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WardSummary>> GetWardsAsync(CancellationToken cancellationToken);
}
