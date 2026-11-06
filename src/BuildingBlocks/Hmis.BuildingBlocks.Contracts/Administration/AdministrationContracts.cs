namespace Hmis.BuildingBlocks.Contracts.Administration;

// Contracts owned and implemented by the Administration module, consumed by other modules via DI.

public sealed record DepartmentSummary(Guid Id, string Code, string Name, Guid? ParentDepartmentId, bool IsActive);

public sealed record DesignationSummary(Guid Id, string Name, string Category, bool IsActive);

public sealed record HospitalSettingsSummary(
    string HospitalName,
    string TimeZone,
    string CurrencyCode,
    string CurrencySymbol,
    int FinancialYearStartMonth);

public interface IDepartmentLookup
{
    Task<DepartmentSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DepartmentSummary>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>All active departments, by name.</summary>
    Task<IReadOnlyList<DepartmentSummary>> GetActiveAsync(CancellationToken cancellationToken);
}

public interface IDesignationLookup
{
    Task<DesignationSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DesignationSummary>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>All active designations, by name.</summary>
    Task<IReadOnlyList<DesignationSummary>> GetActiveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Extension point implemented by modules that reference departments (e.g. HR). Administration asks every guard
/// before deactivating a department, without depending on those modules (keeps module dependencies one-way).
/// </summary>
public interface IDepartmentUsageGuard
{
    /// <summary>A user-facing reason the department cannot be deactivated, or null if it can.</summary>
    Task<string?> GetDeactivationBlockerAsync(Guid departmentId, CancellationToken cancellationToken);
}

public interface IHospitalSettingsProvider
{
    Task<HospitalSettingsSummary> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Allocates the next formatted number for a sequence (e.g. "MR26000123").
/// Each call is atomic and unique; numbers may have gaps if the caller's own transaction later fails.
/// </summary>
public interface INumberSequenceService
{
    Task<string> NextAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Default definition of a number sequence a module needs. Created once at startup; admins may change it later.</summary>
public sealed record NumberSequenceDefinition(
    string Key,
    string Name,
    string Prefix,
    string YearFormat,
    int PadLength,
    string ResetPolicy);

/// <summary>Modules declare the number sequences they need; the Administration module creates missing ones at startup.</summary>
public interface INumberSequenceDefinitionProvider
{
    IEnumerable<NumberSequenceDefinition> GetSequences();
}
