using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hmis.Modules.Administration.Application.Services;

/// <summary>Contract implementations consumed by other modules.</summary>
public sealed class AdministrationLookups(
    IDepartmentRepository departments,
    IDesignationRepository designations,
    IHospitalProfileRepository profiles,
    ISystemSettingsRepository settings) : IDepartmentLookup, IDesignationLookup, IHospitalSettingsProvider
{
    async Task<DepartmentSummary?> IDepartmentLookup.GetAsync(Guid id, CancellationToken cancellationToken) =>
        await departments.GetByIdAsync(id, cancellationToken) is { } d
            ? new DepartmentSummary(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.IsActive)
            : null;

    async Task<IReadOnlyList<DepartmentSummary>> IDepartmentLookup.GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        (await departments.GetByIdsAsync(ids, cancellationToken))
            .Select(d => new DepartmentSummary(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.IsActive))
            .ToList();

    async Task<IReadOnlyList<DesignationSummary>> IDesignationLookup.GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        (await designations.GetByIdsAsync(ids, cancellationToken))
            .Select(d => new DesignationSummary(d.Id, d.Name, d.Category.ToString(), d.IsActive))
            .ToList();

    async Task<DesignationSummary?> IDesignationLookup.GetAsync(Guid id, CancellationToken cancellationToken) =>
        await designations.GetByIdAsync(id, cancellationToken) is { } d
            ? new DesignationSummary(d.Id, d.Name, d.Category.ToString(), d.IsActive)
            : null;

    async Task<HospitalSettingsSummary> IHospitalSettingsProvider.GetAsync(CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(cancellationToken);
        var s = await settings.GetAsync(cancellationToken) ?? SystemSettings.CreateDefault();
        return new HospitalSettingsSummary(profile?.Name ?? string.Empty, s.TimeZone, s.CurrencyCode, s.CurrencySymbol, s.FinancialYearStartMonth);
    }
}

public sealed class AdministrationSeedOptions
{
    public const string SectionName = "Administration";

    /// <summary>Initial hospital name used when the profile is first created. Editable afterwards.</summary>
    public string HospitalName { get; set; } = "Hospital";
}

/// <summary>The numbering the BRD places under Administration configuration (Module 01). Proposed formats, editable.</summary>
public sealed class AdministrationNumberSequences : INumberSequenceDefinitionProvider
{
    public const string PatientMrn = "PATIENT_MRN";
    public const string Invoice = "INVOICE";
    public const string Appointment = "APPOINTMENT";

    public IEnumerable<NumberSequenceDefinition> GetSequences() =>
    [
        new(PatientMrn, "Patient MR number", "MR", nameof(YearFormat.TwoDigit), 6, nameof(SequenceResetPolicy.Never)),
        new(Invoice, "Invoice number", "INV", nameof(YearFormat.TwoDigit), 6, nameof(SequenceResetPolicy.Yearly)),
        new(Appointment, "Appointment number", "APT", nameof(YearFormat.TwoDigit), 6, nameof(SequenceResetPolicy.Yearly))
    ];
}

/// <summary>
/// Idempotent startup seeding: hospital profile, settings (July–June financial year), standard departments and
/// designations on first run only, and any number sequences declared by modules that do not exist yet.
/// </summary>
public sealed class AdministrationSeedTask(
    IHospitalProfileRepository profiles,
    ISystemSettingsRepository settings,
    IDepartmentRepository departments,
    IDesignationRepository designations,
    INumberSequenceRepository sequences,
    IEnumerable<INumberSequenceDefinitionProvider> sequenceProviders,
    IAdministrationUnitOfWork unitOfWork,
    IDateTimeProvider dateTime,
    IOptions<AdministrationSeedOptions> options,
    ILogger<AdministrationSeedTask> logger) : IStartupTask
{
    private static readonly (string Code, string Name, DepartmentType Type)[] DefaultDepartments =
    [
        ("MED", "Medicine", DepartmentType.Clinical),
        ("SURG", "Surgery", DepartmentType.Clinical),
        ("GYN", "Gynaecology & Obstetrics", DepartmentType.Clinical),
        ("PAED", "Paediatrics", DepartmentType.Clinical),
        ("ORTHO", "Orthopaedics", DepartmentType.Clinical),
        ("CARD", "Cardiology", DepartmentType.Clinical),
        ("ENT", "ENT", DepartmentType.Clinical),
        ("EYE", "Ophthalmology (Eye)", DepartmentType.Clinical),
        ("EMR", "Emergency", DepartmentType.Clinical),
        ("LAB", "Laboratory", DepartmentType.Diagnostic),
        ("RAD", "Radiology & Imaging", DepartmentType.Diagnostic),
        ("PHAR", "Pharmacy", DepartmentType.Support),
        ("ADMIN", "Administration", DepartmentType.Administrative),
        ("ACC", "Accounts", DepartmentType.Administrative)
    ];

    private static readonly (string Name, DesignationCategory Category)[] DefaultDesignations =
    [
        ("Consultant", DesignationCategory.Doctor),
        ("Medical Officer", DesignationCategory.Doctor),
        ("Resident Doctor", DesignationCategory.Doctor),
        ("Senior Staff Nurse", DesignationCategory.Nurse),
        ("Staff Nurse", DesignationCategory.Nurse),
        ("Medical Technologist", DesignationCategory.Technician),
        ("Radiographer", DesignationCategory.Technician),
        ("Pharmacist", DesignationCategory.Pharmacist),
        ("Receptionist", DesignationCategory.Administrative),
        ("Accountant", DesignationCategory.Administrative),
        ("Administrator", DesignationCategory.Administrative),
        ("Ward Boy / Aya", DesignationCategory.Support)
    ];

    public int Order => 10;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (await profiles.GetAsync(cancellationToken) is null)
        {
            await profiles.AddAsync(HospitalProfile.Create(options.Value.HospitalName), cancellationToken);
            logger.LogInformation("Seeded hospital profile");
        }

        if (await settings.GetAsync(cancellationToken) is null)
        {
            await settings.AddAsync(SystemSettings.CreateDefault(), cancellationToken);
            logger.LogInformation("Seeded system settings (Asia/Dhaka, BDT, July–June financial year)");
        }

        if (!await departments.AnyAsync(cancellationToken))
        {
            foreach (var (code, name, type) in DefaultDepartments)
            {
                await departments.AddAsync(Department.Create(code, name, type, null, null), cancellationToken);
            }

            logger.LogInformation("Seeded {Count} standard departments", DefaultDepartments.Length);
        }

        if (!await designations.AnyAsync(cancellationToken))
        {
            foreach (var (name, category) in DefaultDesignations)
            {
                await designations.AddAsync(Designation.Create(name, category), cancellationToken);
            }

            logger.LogInformation("Seeded {Count} standard designations", DefaultDesignations.Length);
        }

        var existing = (await sequences.GetAllAsync(cancellationToken)).Select(s => s.Key).ToHashSet();
        foreach (var definition in sequenceProviders.SelectMany(p => p.GetSequences()))
        {
            if (existing.Add(definition.Key.ToUpperInvariant()))
            {
                await sequences.AddAsync(NumberSequence.Create(
                    definition.Key,
                    definition.Name,
                    definition.Prefix,
                    Enum.Parse<YearFormat>(definition.YearFormat),
                    definition.PadLength,
                    Enum.Parse<SequenceResetPolicy>(definition.ResetPolicy),
                    dateTime.LocalToday.Year), cancellationToken);
                logger.LogInformation("Seeded number sequence {Key}", definition.Key);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
