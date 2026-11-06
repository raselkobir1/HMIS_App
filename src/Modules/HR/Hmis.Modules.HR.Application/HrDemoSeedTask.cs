using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Seeding;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.Modules.HR.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hmis.Modules.HR.Application;

/// <summary>
/// Demo staff for development and training installs (DemoData:Enabled). One current employee per role, with a doctor
/// profile for the doctor, under well-known ids so the Identity demo logins can link to them. Runs after Administration
/// has seeded departments and designations; idempotent, and skipped entirely when demo data is off.
/// </summary>
public sealed class HrDemoSeedTask(
    IEmployeeRepository employees,
    IDoctorProfileRepository doctors,
    IHrUnitOfWork unitOfWork,
    IDepartmentLookup departmentLookup,
    IDesignationLookup designationLookup,
    INumberSequenceService numberSequences,
    IDateTimeProvider dateTime,
    IOptions<DemoDataOptions> options,
    ILogger<HrDemoSeedTask> logger) : IStartupTask
{
    public const string DoctorRegistrationNumber = "DEMO-0001";

    private sealed record DemoEmployee(
        Guid Id, string FullName, Gender Gender, string Mobile, string DepartmentCode, string DesignationCategory, string DesignationName);

    private static readonly DemoEmployee[] Staff =
    [
        new(DemoDataIds.DoctorEmployeeId, "Dr. Demo Doctor", Gender.Male, "01700000001", "MED", "Doctor", "Consultant"),
        new(DemoDataIds.ReceptionistEmployeeId, "Demo Receptionist", Gender.Female, "01700000002", "ADMIN", "Administrative", "Receptionist"),
        new(DemoDataIds.NurseEmployeeId, "Demo Nurse", Gender.Female, "01700000003", "MED", "Nurse", "Staff Nurse"),
        new(DemoDataIds.CashierEmployeeId, "Demo Cashier", Gender.Male, "01700000004", "ACC", "Administrative", "Accountant"),
        new(DemoDataIds.AdministratorEmployeeId, "Demo Administrator", Gender.Male, "01700000005", "ADMIN", "Administrative", "Administrator")
    ];

    public int Order => 30;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var departments = await departmentLookup.GetActiveAsync(cancellationToken);
        var designations = await designationLookup.GetActiveAsync(cancellationToken);
        if (departments.Count == 0 || designations.Count == 0)
        {
            logger.LogWarning("Demo staff not seeded: no active departments or designations exist yet");
            return;
        }

        var created = 0;
        foreach (var staff in Staff)
        {
            var employee = await employees.GetByIdAsync(staff.Id, cancellationToken);
            if (employee is null)
            {
                var department = departments.FirstOrDefault(d => d.Code == staff.DepartmentCode) ?? departments[0];
                var designation = designations.FirstOrDefault(d => d.Name == staff.DesignationName)
                    ?? designations.FirstOrDefault(d => d.Category == staff.DesignationCategory)
                    ?? designations[0];

                var code = await numberSequences.NextAsync(HrNumberSequences.EmployeeCode, cancellationToken);
                employee = Employee.Create(code, staff.FullName, staff.Gender, null, staff.Mobile, null, null,
                    department.Id, designation.Id, EmploymentType.Permanent, dateTime.LocalToday, staff.Id);
                await employees.AddAsync(employee, cancellationToken);
                created++;
            }

            if (staff.Id == DemoDataIds.DoctorEmployeeId
                && await doctors.GetByEmployeeIdAsync(staff.Id, cancellationToken) is null
                && !await doctors.RegistrationExistsAsync(DoctorRegistrationNumber, null, cancellationToken))
            {
                await doctors.AddAsync(DoctorProfile.Create(staff.Id, "General Medicine", "MBBS, FCPS (Medicine)",
                    DoctorRegistrationNumber, 500, 300, 200), cancellationToken);
                created++;
            }
        }

        if (created > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded demo staff ({Count} records)", created);
        }
    }
}
