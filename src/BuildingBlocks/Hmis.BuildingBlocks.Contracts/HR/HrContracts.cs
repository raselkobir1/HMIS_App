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
