using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;

namespace Hmis.Modules.Administration.Domain.Entities;

/// <summary>The hospital's identity and registration details. Exactly one row (single facility, decision D3).</summary>
[Audited]
public sealed class HospitalProfile : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private HospitalProfile() { }

    public string Name { get; private set; } = string.Empty;
    public string? NameBangla { get; private set; }
    public string? ShortName { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? LicenseNumber { get; private set; }

    /// <summary>Business Identification Number / VAT registration.</summary>
    public string? TaxId { get; private set; }

    public string? OpeningHours { get; private set; }
    public string? LogoPath { get; private set; }
    public string? LogoContentType { get; private set; }
    public uint RowVersion { get; private set; }

    public static HospitalProfile Create(string name) => new() { Name = name.Trim() };

    public void Update(
        string name,
        string? nameBangla,
        string? shortName,
        string? address,
        string? phone,
        string? email,
        string? website,
        string? registrationNumber,
        string? licenseNumber,
        string? taxId,
        string? openingHours)
    {
        Name = name.Trim();
        NameBangla = Clean(nameBangla);
        ShortName = Clean(shortName);
        Address = Clean(address);
        Phone = Clean(phone);
        Email = Clean(email);
        Website = Clean(website);
        RegistrationNumber = Clean(registrationNumber);
        LicenseNumber = Clean(licenseNumber);
        TaxId = Clean(taxId);
        OpeningHours = Clean(openingHours);
    }

    public void SetLogo(string? path, string? contentType)
    {
        LogoPath = path;
        LogoContentType = contentType;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
