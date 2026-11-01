using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.HR.Domain.Entities;

/// <summary>
/// Clinical profile of an employee who is a doctor (BRD Module 03). One profile per employee.
/// Fees are the defaults used when booking; the appointment keeps its own snapshot for billing.
/// </summary>
[Audited]
public sealed class DoctorProfile : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private DoctorProfile() { }

    public Guid EmployeeId { get; private set; }
    public string Specialization { get; private set; } = string.Empty;
    public string Qualifications { get; private set; } = string.Empty;

    /// <summary>Bangladesh Medical &amp; Dental Council (BMDC) registration number.</summary>
    public string RegistrationNumber { get; private set; } = string.Empty;

    public decimal NewPatientFee { get; private set; }
    public decimal FollowUpFee { get; private set; }

    /// <summary>Charged to an inpatient's running bill once per day when this doctor signs a ward-round or consultant-review note.</summary>
    public decimal InpatientVisitFee { get; private set; }

    public bool IsAcceptingAppointments { get; private set; } = true;
    public uint RowVersion { get; private set; }

    public static DoctorProfile Create(
        Guid employeeId, string specialization, string qualifications, string registrationNumber,
        decimal newPatientFee, decimal followUpFee, decimal inpatientVisitFee)
    {
        var profile = new DoctorProfile { EmployeeId = employeeId };
        profile.Update(specialization, qualifications, registrationNumber, newPatientFee, followUpFee, inpatientVisitFee, true);
        return profile;
    }

    public void Update(
        string specialization, string qualifications, string registrationNumber,
        decimal newPatientFee, decimal followUpFee, decimal inpatientVisitFee, bool isAcceptingAppointments)
    {
        if (newPatientFee < 0 || followUpFee < 0 || inpatientVisitFee < 0)
        {
            throw new DomainException("DOCTOR_FEE_NEGATIVE", "Fees cannot be negative.");
        }

        Specialization = specialization.Trim();
        Qualifications = qualifications.Trim();
        RegistrationNumber = NormalizeRegistration(registrationNumber);
        NewPatientFee = decimal.Round(newPatientFee, 2);
        FollowUpFee = decimal.Round(followUpFee, 2);
        InpatientVisitFee = decimal.Round(inpatientVisitFee, 2);
        IsAcceptingAppointments = isAcceptingAppointments;
    }

    public static string NormalizeRegistration(string value) => value.Trim().ToUpperInvariant();
}
