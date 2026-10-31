using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Administration.Domain.Entities;

namespace Hmis.Modules.Administration.Application.DTOs;

// ---------- Hospital profile & settings ----------

public sealed record HospitalProfileDto(
    string Name,
    string? NameBangla,
    string? ShortName,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? RegistrationNumber,
    string? LicenseNumber,
    string? TaxId,
    string? OpeningHours,
    bool HasLogo,
    uint RowVersion);

public sealed record HospitalProfileUpdateDto
{
    public string Name { get; init; } = string.Empty;
    public string? NameBangla { get; init; }
    public string? ShortName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? LicenseNumber { get; init; }
    public string? TaxId { get; init; }
    public string? OpeningHours { get; init; }
    public uint RowVersion { get; init; }
}

/// <summary>Public, non-sensitive branding for the login page and header.</summary>
/// <summary>Public hospital identity for the login page, app shell and printed letterheads (contact details are public).</summary>
public sealed record BrandingDto(string Name, string? ShortName, bool HasLogo, string? LogoVersion, string? Address, string? Phone, string? Email);

public sealed record LogoFile(Stream Content, string ContentType);

public sealed record SystemSettingsDto(
    string TimeZone,
    string CurrencyCode,
    string CurrencySymbol,
    int FinancialYearStartMonth,
    uint RowVersion);

public sealed record SystemSettingsUpdateDto
{
    public string CurrencyCode { get; init; } = string.Empty;
    public string CurrencySymbol { get; init; } = string.Empty;
    public int FinancialYearStartMonth { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record StatusUpdateDto
{
    public bool IsActive { get; init; }
}

// ---------- Departments ----------

public sealed class DepartmentListQuery : PaginationRequest
{
    public bool? IsActive { get; set; }
    public DepartmentType? Type { get; set; }
    public Guid? ParentDepartmentId { get; set; }
}

public sealed record DepartmentDto(
    Guid Id,
    string Code,
    string Name,
    DepartmentType Type,
    Guid? ParentDepartmentId,
    string? ParentName,
    string? Description,
    bool IsActive,
    int SubDepartmentCount,
    uint RowVersion);

public sealed record DepartmentLookupItemDto(Guid Id, string Code, string Name, Guid? ParentDepartmentId, DepartmentType Type);

public sealed record DepartmentCreateDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DepartmentType Type { get; init; }
    public Guid? ParentDepartmentId { get; init; }
    public string? Description { get; init; }
}

public sealed record DepartmentUpdateDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DepartmentType Type { get; init; }
    public Guid? ParentDepartmentId { get; init; }
    public string? Description { get; init; }
    public uint RowVersion { get; init; }
}

// ---------- Designations ----------

public sealed class DesignationListQuery : PaginationRequest
{
    public bool? IsActive { get; set; }
    public DesignationCategory? Category { get; set; }
}

public sealed record DesignationDto(Guid Id, string Name, DesignationCategory Category, bool IsActive, uint RowVersion);

public sealed record DesignationLookupItemDto(Guid Id, string Name, DesignationCategory Category);

public sealed record DesignationCreateDto
{
    public string Name { get; init; } = string.Empty;
    public DesignationCategory Category { get; init; }
}

public sealed record DesignationUpdateDto
{
    public string Name { get; init; } = string.Empty;
    public DesignationCategory Category { get; init; }
    public uint RowVersion { get; init; }
}

// ---------- Number sequences ----------

public sealed record NumberSequenceDto(
    string Key,
    string Name,
    string Prefix,
    YearFormat YearFormat,
    int PadLength,
    SequenceResetPolicy ResetPolicy,
    long CurrentValue,
    string NextNumberPreview,
    uint RowVersion);

public sealed record NumberSequenceUpdateDto
{
    public string Prefix { get; init; } = string.Empty;
    public YearFormat YearFormat { get; init; }
    public int PadLength { get; init; }
    public SequenceResetPolicy ResetPolicy { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record NumberSequenceNextValueDto
{
    public long NextValue { get; init; }
}
