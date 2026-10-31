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
}

public interface IDesignationLookup
{
    Task<DesignationSummary?> GetAsync(Guid id, CancellationToken cancellationToken);
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
