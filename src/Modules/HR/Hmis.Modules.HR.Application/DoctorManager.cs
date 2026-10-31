using FluentValidation;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Application.Validators;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.Modules.HR.Domain.Entities;

namespace Hmis.Modules.HR.Application;

public static class DoctorPermissions
{
    public const string View = "HR.Doctors.View";
    public const string Manage = "HR.Doctors.Manage";
}

public sealed class DoctorPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(DoctorPermissions.View, HrPermissions.Module, "View doctor profiles and fees",
            [SystemRoles.HospitalAdministrator, SystemRoles.Receptionist, SystemRoles.Doctor, SystemRoles.Nurse,
             SystemRoles.HrManager, SystemRoles.Accountant, SystemRoles.HospitalOwner]),
        new(DoctorPermissions.Manage, HrPermissions.Module, "Create and edit doctor profiles and consultation fees",
            [SystemRoles.HospitalAdministrator, SystemRoles.HrManager])
    ];
}

// ---------- DTOs ----------

public sealed class DoctorListQuery : PaginationRequest
{
    public Guid? DepartmentId { get; set; }
    public bool? IsAcceptingAppointments { get; set; }
}

public sealed record DoctorDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    string FullName,
    Guid DepartmentId,
    string? DepartmentName,
    string? DesignationName,
    string Specialization,
    string Qualifications,
    string RegistrationNumber,
    decimal NewPatientFee,
    decimal FollowUpFee,
    bool IsAcceptingAppointments,
    EmploymentStatus EmploymentStatus,
    uint RowVersion);

public sealed record DoctorCreateDto
{
    public Guid EmployeeId { get; init; }
    public string Specialization { get; init; } = string.Empty;
    public string Qualifications { get; init; } = string.Empty;
    public string RegistrationNumber { get; init; } = string.Empty;
    public decimal NewPatientFee { get; init; }
    public decimal FollowUpFee { get; init; }
}

public sealed record DoctorUpdateDto
{
    public string Specialization { get; init; } = string.Empty;
    public string Qualifications { get; init; } = string.Empty;
    public string RegistrationNumber { get; init; } = string.Empty;
    public decimal NewPatientFee { get; init; }
    public decimal FollowUpFee { get; init; }
    public bool IsAcceptingAppointments { get; init; }
    public uint RowVersion { get; init; }
}

// ---------- Validators ----------

public sealed class DoctorCreateValidator : AbstractValidator<DoctorCreateDto>
{
    public DoctorCreateValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Specialization).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Qualifications).NotEmpty().MaximumLength(250);
        RuleFor(x => x.RegistrationNumber).DoctorRegistration();
        RuleFor(x => x.NewPatientFee).InclusiveBetween(0, 1_000_000).PrecisionScale(18, 2, true);
        RuleFor(x => x.FollowUpFee).InclusiveBetween(0, 1_000_000).PrecisionScale(18, 2, true);
    }
}

public sealed class DoctorUpdateValidator : AbstractValidator<DoctorUpdateDto>
{
    public DoctorUpdateValidator()
    {
        RuleFor(x => x.Specialization).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Qualifications).NotEmpty().MaximumLength(250);
        RuleFor(x => x.RegistrationNumber).DoctorRegistration();
        RuleFor(x => x.NewPatientFee).InclusiveBetween(0, 1_000_000).PrecisionScale(18, 2, true);
        RuleFor(x => x.FollowUpFee).InclusiveBetween(0, 1_000_000).PrecisionScale(18, 2, true);
    }
}

public sealed class DoctorListQueryValidator : PaginationRequestValidator<DoctorListQuery>
{
    public DoctorListQueryValidator() =>
        RuleFor(x => x.SortBy).Empty().WithMessage("Doctors are sorted by name.");
}

internal static class DoctorRules
{
    public static IRuleBuilderOptions<T, string> DoctorRegistration<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(30).Matches(@"^[A-Za-z0-9\-/ ]+$")
            .WithMessage("Registration number may contain only letters, digits, spaces, '-' and '/'.");
}

// ---------- Repository & manager ----------

public interface IDoctorProfileRepository : BuildingBlocks.Application.Abstractions.IRepository<DoctorProfile>,
    BuildingBlocks.Application.Abstractions.IConcurrencyRepository<DoctorProfile>
{
    Task<DoctorProfile?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken);

    Task<bool> RegistrationExistsAsync(string registrationNumber, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Profiles with their employee, filtered and paged (both in the hr schema).</summary>
    Task<PagedResponse<(DoctorProfile Profile, Employee Employee)>> SearchAsync(DoctorListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<(DoctorProfile Profile, Employee Employee)>> GetByEmployeeIdsAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken);
}

public interface IDoctorManager
{
    Task<PagedResponse<DoctorDto>> SearchAsync(DoctorListQuery query, CancellationToken cancellationToken);

    Task<DoctorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<DoctorDto> CreateAsync(DoctorCreateDto request, CancellationToken cancellationToken);

    Task<DoctorDto> UpdateAsync(Guid id, DoctorUpdateDto request, CancellationToken cancellationToken);
}

public sealed class DoctorManager(
    IDoctorProfileRepository doctors,
    IEmployeeRepository employees,
    IHrUnitOfWork unitOfWork,
    IDepartmentLookup departmentLookup,
    IDesignationLookup designationLookup) : IDoctorManager, IDoctorLookup
{
    public const string DoctorDesignationCategory = "Doctor";

    public async Task<PagedResponse<DoctorDto>> SearchAsync(DoctorListQuery query, CancellationToken cancellationToken)
    {
        var page = await doctors.SearchAsync(query, cancellationToken);
        var names = await LoadNamesAsync(page.Items.Select(i => i.Employee), cancellationToken);
        return page.Map(i => ToDto(i.Profile, i.Employee, names));
    }

    public async Task<DoctorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(id, cancellationToken);
        var employee = await employees.GetByIdAsync(profile.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), profile.EmployeeId);
        return ToDto(profile, employee, await LoadNamesAsync([employee], cancellationToken));
    }

    public async Task<DoctorDto> CreateAsync(DoctorCreateDto request, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is not { IsCurrent: true })
        {
            throw new BusinessException("DOCTOR_EMPLOYEE_INVALID", "Select a current employee.", "employeeId");
        }

        // Only staff with a Doctor-category designation can have a doctor profile.
        var designation = await designationLookup.GetAsync(employee.DesignationId, cancellationToken);
        if (designation?.Category != DoctorDesignationCategory)
        {
            throw new BusinessException("DOCTOR_EMPLOYEE_NOT_DOCTOR",
                "This employee's designation is not in the Doctor category.", "employeeId");
        }

        if (await doctors.GetByEmployeeIdAsync(employee.Id, cancellationToken) is not null)
        {
            throw new ConflictException("This employee already has a doctor profile.", "DOCTOR_PROFILE_EXISTS");
        }

        await EnsureRegistrationUniqueAsync(request.RegistrationNumber, null, cancellationToken);

        var profile = DoctorProfile.Create(employee.Id, request.Specialization, request.Qualifications,
            request.RegistrationNumber, request.NewPatientFee, request.FollowUpFee);
        await doctors.AddAsync(profile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(profile, employee, await LoadNamesAsync([employee], cancellationToken));
    }

    public async Task<DoctorDto> UpdateAsync(Guid id, DoctorUpdateDto request, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(id, cancellationToken);
        doctors.SetExpectedRowVersion(profile, request.RowVersion);
        await EnsureRegistrationUniqueAsync(request.RegistrationNumber, id, cancellationToken);

        profile.Update(request.Specialization, request.Qualifications, request.RegistrationNumber,
            request.NewPatientFee, request.FollowUpFee, request.IsAcceptingAppointments);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ---------- IDoctorLookup (contract; DoctorId = employee id) ----------

    public async Task<DoctorSummary?> GetAsync(Guid doctorId, CancellationToken cancellationToken) =>
        (await GetManyAsync([doctorId], cancellationToken)).FirstOrDefault();

    public async Task<IReadOnlyList<DoctorSummary>> GetManyAsync(IReadOnlyCollection<Guid> doctorIds, CancellationToken cancellationToken) =>
        (await doctors.GetByEmployeeIdsAsync(doctorIds, cancellationToken))
            .Select(i => new DoctorSummary(
                i.Employee.Id, i.Employee.EmployeeCode, i.Employee.FullName, i.Employee.DepartmentId,
                i.Profile.Specialization, i.Profile.Qualifications, i.Profile.NewPatientFee, i.Profile.FollowUpFee,
                i.Profile.IsAcceptingAppointments, i.Employee.IsCurrent))
            .ToList();

    private async Task<DoctorProfile> GetProfileAsync(Guid id, CancellationToken cancellationToken) =>
        await doctors.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Doctor profile", id);

    private async Task EnsureRegistrationUniqueAsync(string registrationNumber, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await doctors.RegistrationExistsAsync(DoctorProfile.NormalizeRegistration(registrationNumber), excludeId, cancellationToken))
        {
            throw new ConflictException("Another doctor already has this registration number.", "DOCTOR_REGISTRATION_TAKEN");
        }
    }

    private async Task<(Dictionary<Guid, string> Departments, Dictionary<Guid, string> Designations)> LoadNamesAsync(
        IEnumerable<Employee> list, CancellationToken cancellationToken)
    {
        var items = list.ToList();
        var departments = await departmentLookup.GetManyAsync(items.Select(e => e.DepartmentId).Distinct().ToList(), cancellationToken);
        var designations = await designationLookup.GetManyAsync(items.Select(e => e.DesignationId).Distinct().ToList(), cancellationToken);
        return (departments.ToDictionary(d => d.Id, d => d.Name), designations.ToDictionary(d => d.Id, d => d.Name));
    }

    private static DoctorDto ToDto(
        DoctorProfile p, Employee e, (Dictionary<Guid, string> Departments, Dictionary<Guid, string> Designations) names) =>
        new(p.Id, e.Id, e.EmployeeCode, e.FullName, e.DepartmentId, names.Departments.GetValueOrDefault(e.DepartmentId),
            names.Designations.GetValueOrDefault(e.DesignationId), p.Specialization, p.Qualifications, p.RegistrationNumber,
            p.NewPatientFee, p.FollowUpFee, p.IsAcceptingAppointments, e.Status, p.RowVersion);
}
