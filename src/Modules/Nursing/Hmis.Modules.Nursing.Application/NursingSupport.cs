using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Auditing;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Clinical;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.BuildingBlocks.Contracts.Ipd;
using Hmis.BuildingBlocks.Contracts.Patient;

namespace Hmis.Modules.Nursing.Application;

/// <summary>Lookups shared by the nursing managers: the stay, the patient header, names and audit.</summary>
public sealed class NursingSupport(
    IInpatientLookup inpatients,
    IMedicationOrderLookup orderLookup,
    IPatientLookup patientLookup,
    IPatientClinicalSummary clinicalSummary,
    IDoctorLookup doctorLookup,
    IUserEmployeeLookup userLookup,
    ICurrentUser currentUser,
    IAuditService auditService)
{
    public Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<InpatientStay> GetStayAsync(Guid admissionId, CancellationToken cancellationToken) =>
        await inpatients.GetAsync(admissionId, cancellationToken) ?? throw new NotFoundException("Admission", admissionId);

    /// <summary>
    /// The stay for reading nursing records (Q-NUR-5). While the patient is in hospital any holder of Nursing.View may read.
    /// After discharge the records are closed to nurses and other staff; only a doctor who treated the patient (attending,
    /// or a duty doctor who ordered medicines on this stay) may still open them.
    /// </summary>
    public async Task<InpatientStay> GetViewableStayAsync(Guid admissionId, CancellationToken cancellationToken)
    {
        var stay = await GetStayAsync(admissionId, cancellationToken);
        if (stay.IsActive)
        {
            return stay;
        }

        var employeeId = await GetCurrentEmployeeIdAsync(cancellationToken);
        var doctorId = employeeId is { } id && await doctorLookup.GetAsync(id, cancellationToken) is not null ? id : (Guid?)null;
        if (doctorId is { } doctor
            && (await inpatients.IsAttendingDoctorAsync(doctor, stay.PatientId, cancellationToken)
                || (await orderLookup.GetForAdmissionsAsync([admissionId], cancellationToken)).Any(o => o.OrderedByDoctorId == doctor)))
        {
            return stay;
        }

        throw new ForbiddenException(doctorId is null
            ? "The patient has been discharged; nursing records are closed. Only the doctors who treated the patient can open them."
            : "After discharge, a doctor can open the nursing records only of patients they treated.");
    }

    /// <summary>Nursing records are written only while the patient is in hospital.</summary>
    public async Task<InpatientStay> GetActiveStayAsync(Guid admissionId, CancellationToken cancellationToken)
    {
        var stay = await GetStayAsync(admissionId, cancellationToken);
        return stay.IsActive
            ? stay
            : throw new BusinessException(NursingErrorCodes.StayNotActive, "The patient has been discharged; nursing records are closed.");
    }

    public async Task<NursingPatientDto> GetPatientAsync(InpatientStay stay, CancellationToken cancellationToken)
    {
        var patient = await patientLookup.GetAsync(stay.PatientId, cancellationToken);
        var summary = await clinicalSummary.GetAsync(stay.PatientId, cancellationToken);
        var doctor = await doctorLookup.GetAsync(stay.AttendingDoctorId, cancellationToken);
        return new NursingPatientDto(stay.AdmissionId, stay.AdmissionNumber, stay.Status, stay.IsActive, stay.PatientId, patient?.FullName,
            patient?.MRNumber, patient?.Gender, patient?.DateOfBirth, stay.WardName, stay.BedNumber, doctor?.FullName, stay.AdmittedAt,
            summary.Allergies.Select(a => new AllergyDto(a.Allergen, a.Type, a.Severity, a.Reaction)).ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken) =>
        await userLookup.GetFullNamesAsync(userIds.OfType<Guid>().Distinct().ToList(), cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetDoctorNamesAsync(IEnumerable<Guid> doctorIds, CancellationToken cancellationToken) =>
        (await doctorLookup.GetManyAsync(doctorIds.Distinct().ToList(), cancellationToken)).ToDictionary(d => d.DoctorId, d => d.FullName);

    public Task<Guid?> GetCurrentEmployeeIdAsync(CancellationToken cancellationToken) => userLookup.GetEmployeeIdAsync(UserId, cancellationToken);

    public Task AuditViewAsync(string entityName, Guid id, CancellationToken cancellationToken) =>
        auditService.RecordImmediateAsync(new AuditEvent("View", NursingPermissions.Module, entityName, id.ToString()), cancellationToken);

    public static string? NameOf(Guid? userId, IReadOnlyDictionary<Guid, string> names) =>
        userId is { } id ? names.GetValueOrDefault(id) : null;
}
