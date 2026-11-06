using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.HR.Domain.Entities;

public enum Gender
{
    Male,
    Female,
    Other
}

public enum EmploymentType
{
    Permanent,
    Contract,
    Visiting,
    Intern
}

public enum EmploymentStatus
{
    Active,
    OnLeave,
    Resigned,
    Terminated
}

/// <summary>
/// Basic employee record (Phase 1). Department and designation are owned by Administration and referenced by id only.
/// Employees are never deleted; leaving staff are marked Resigned or Terminated with an exit date.
/// </summary>
[Audited]
public sealed class Employee : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    public const int MinMobileDigits = 6;

    private Employee() { }

    /// <summary>Generated from the EMPLOYEE_CODE number sequence; never changes.</summary>
    public string EmployeeCode { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;
    public Gender Gender { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string MobileNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid DesignationId { get; private set; }
    public EmploymentType EmploymentType { get; private set; }
    public DateOnly JoiningDate { get; private set; }
    public EmploymentStatus Status { get; private set; } = EmploymentStatus.Active;
    public DateOnly? ExitDate { get; private set; }
    public uint RowVersion { get; private set; }

    /// <summary>Active or on leave — still part of the workforce.</summary>
    public bool IsCurrent => Status is EmploymentStatus.Active or EmploymentStatus.OnLeave;

    public static Employee Create(
        string employeeCode,
        string fullName,
        Gender gender,
        DateOnly? dateOfBirth,
        string mobileNumber,
        string? email,
        string? address,
        Guid departmentId,
        Guid designationId,
        EmploymentType employmentType,
        DateOnly joiningDate,
        Guid? id = null)
    {
        var employee = new Employee { EmployeeCode = employeeCode };
        if (id is { } fixedId)
        {
            // Only startup seeding passes an id, so demo records keep a well-known identity across restarts.
            employee.Id = fixedId;
        }

        employee.Update(fullName, gender, dateOfBirth, mobileNumber, email, address, departmentId, designationId, employmentType, joiningDate);
        return employee;
    }

    public void Update(
        string fullName,
        Gender gender,
        DateOnly? dateOfBirth,
        string mobileNumber,
        string? email,
        string? address,
        Guid departmentId,
        Guid designationId,
        EmploymentType employmentType,
        DateOnly joiningDate)
    {
        if (dateOfBirth.HasValue && dateOfBirth.Value >= joiningDate)
        {
            throw new DomainException("EMPLOYEE_DOB_AFTER_JOINING", "Date of birth must be before the joining date.");
        }

        if (ExitDate.HasValue && joiningDate > ExitDate.Value)
        {
            throw new DomainException("EMPLOYEE_JOINING_AFTER_EXIT", "Joining date cannot be after the exit date.");
        }

        FullName = fullName.Trim();
        Gender = gender;
        DateOfBirth = dateOfBirth;
        MobileNumber = NormalizeMobile(mobileNumber);
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        DepartmentId = departmentId;
        DesignationId = designationId;
        EmploymentType = employmentType;
        JoiningDate = joiningDate;
    }

    /// <summary>Stored as digits only so "+88 01712-345678" and "01712345678" match in search and lookups.</summary>
    public static string NormalizeMobile(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < MinMobileDigits)
        {
            throw new DomainException("EMPLOYEE_MOBILE_INVALID", $"A mobile number needs at least {MinMobileDigits} digits.");
        }

        return digits;
    }

    /// <summary>
    /// Changes employment status. Leaving (Resigned/Terminated) requires an exit date on or after joining;
    /// returning to Active/OnLeave clears it.
    /// </summary>
    public void ChangeStatus(EmploymentStatus status, DateOnly? exitDate)
    {
        if (status is EmploymentStatus.Resigned or EmploymentStatus.Terminated)
        {
            if (exitDate is null)
            {
                throw new DomainException("EMPLOYEE_EXIT_DATE_REQUIRED", "An exit date is required when an employee leaves.");
            }

            if (exitDate.Value < JoiningDate)
            {
                throw new DomainException("EMPLOYEE_EXIT_BEFORE_JOINING", "The exit date cannot be before the joining date.");
            }

            ExitDate = exitDate;
        }
        else
        {
            ExitDate = null;
        }

        Status = status;
    }
}
