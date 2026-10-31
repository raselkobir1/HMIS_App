namespace Hmis.BuildingBlocks.Contracts.HR;

// Contracts owned and implemented by the HR module.

/// <summary>Minimal employee data for other modules. IsCurrent is false for resigned or terminated employees.</summary>
public sealed record EmployeeSummary(
    Guid Id,
    string EmployeeCode,
    string FullName,
    Guid DepartmentId,
    Guid DesignationId,
    string Status,
    bool IsCurrent);

public interface IEmployeeLookup
{
    Task<EmployeeSummary?> GetAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Doctor data for booking and billing. DoctorId is the doctor's employee id.</summary>
public sealed record DoctorSummary(
    Guid DoctorId,
    string EmployeeCode,
    string FullName,
    Guid DepartmentId,
    string Specialization,
    string Qualifications,
    string RegistrationNumber,
    decimal NewPatientFee,
    decimal FollowUpFee,
    bool IsAcceptingAppointments,
    bool IsCurrent);

public interface IDoctorLookup
{
    Task<DoctorSummary?> GetAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorSummary>> GetManyAsync(IReadOnlyCollection<Guid> doctorIds, CancellationToken cancellationToken);
}
