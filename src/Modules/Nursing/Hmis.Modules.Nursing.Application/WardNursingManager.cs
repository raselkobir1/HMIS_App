using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Clinical;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Ipd;
using Hmis.BuildingBlocks.Contracts.Patient;

namespace Hmis.Modules.Nursing.Application;

/// <summary>The nurse's ward view: the shift now, "my wards" from the roster, and each patient's doses due and overdue.</summary>
public sealed class WardNursingManager(
    INurseAssignmentRepository assignments,
    IMedicationAdministrationRepository administrations,
    IMedicationOrderLookup orderLookup,
    IInpatientLookup inpatients,
    IPatientLookup patientLookup,
    IEmployeeLookup employeeLookup,
    NursingSupport support,
    IDateTimeProvider dateTime) : IWardNursingManager
{
    /// <summary>Missed doses older than this no longer count as overdue on the ward list (they stay visible on the MAR).</summary>
    private static readonly TimeSpan OverdueLookback = TimeSpan.FromHours(24);

    public async Task<CurrentShiftDto> GetCurrentShiftAsync(CancellationToken cancellationToken)
    {
        var (date, shift) = NursingClock.ShiftAt(dateTime.UtcNow, dateTime);
        var (start, end) = NursingClock.ShiftWindow(date, shift, dateTime);
        var wards = (await inpatients.GetWardsAsync(cancellationToken)).Where(w => w.IsActive).OrderBy(w => w.Name).ToList();
        var employeeId = await support.GetCurrentEmployeeIdAsync(cancellationToken);
        var mine = employeeId is { } id
            ? (await assignments.GetForShiftAsync(date, shift, cancellationToken)).Where(a => a.EmployeeId == id).Select(a => a.WardId).ToHashSet()
            : [];
        return new CurrentShiftDto(date, shift, start, end, wards.Where(w => mine.Contains(w.Id)).Select(ToOption).ToList(),
            wards.Select(ToOption).ToList());
    }

    public async Task<WardRoundDto> GetWardAsync(Guid wardId, CancellationToken cancellationToken)
    {
        var ward = (await inpatients.GetWardsAsync(cancellationToken)).FirstOrDefault(w => w.Id == wardId)
            ?? throw new NotFoundException("Ward", wardId);
        var now = dateTime.UtcNow;
        var (date, shift) = NursingClock.ShiftAt(now, dateTime);

        var stays = await inpatients.GetActiveAsync(wardId, cancellationToken);
        var admissionIds = stays.Select(s => s.AdmissionId).ToList();
        var orders = await orderLookup.GetForAdmissionsAsync(admissionIds, cancellationToken);
        var from = now - OverdueLookback;
        var to = now + TimeSpan.FromHours(24);
        var recorded = (await administrations.GetForAdmissionsAsync(admissionIds, from, to, cancellationToken))
            .Where(e => !e.IsEnteredInError && e.ScheduledAt is not null)
            .Select(e => (e.MedicationOrderId, e.ScheduledAt!.Value))
            .ToHashSet();
        var patients = (await patientLookup.GetManyAsync(stays.Select(s => s.PatientId).Distinct().ToList(), cancellationToken)).ToDictionary(p => p.Id);
        var doctors = await support.GetDoctorNamesAsync(stays.Select(s => s.AttendingDoctorId), cancellationToken);

        var rows = stays.Select(stay =>
        {
            var stayOrders = orders.Where(o => o.AdmissionId == stay.AdmissionId).ToList();
            var pending = stayOrders
                .SelectMany(o => NursingClock.Schedule(o, from, to, dateTime)
                    .Where(slot => !recorded.Contains((o.Id, slot)))
                    .Select(slot => (Slot: slot, State: NursingClock.PendingState(o, slot, now))))
                .ToList();
            var patient = patients.GetValueOrDefault(stay.PatientId);
            return new WardPatientDto(stay.AdmissionId, stay.AdmissionNumber, stay.PatientId, patient?.FullName, patient?.MRNumber, patient?.Gender,
                patient?.DateOfBirth, stay.BedNumber, doctors.GetValueOrDefault(stay.AttendingDoctorId), stay.Status, stay.AdmittedAt,
                pending.Count(p => p.State == "Due"), pending.Count(p => p.State == "Overdue"),
                pending.Where(p => p.State == "Upcoming").Select(p => (DateTime?)p.Slot).Min(),
                stayOrders.Count(o => o.Status == "Active"));
        }).ToList();

        var onDuty = new List<OnDutyDto>();
        foreach (var a in (await assignments.GetForShiftAsync(date, shift, cancellationToken)).Where(a => a.WardId == wardId))
        {
            onDuty.Add(new OnDutyDto(a.EmployeeId, (await employeeLookup.GetAsync(a.EmployeeId, cancellationToken))?.FullName, a.IsInCharge));
        }

        return new WardRoundDto(ToOption(ward), date, shift, onDuty.OrderByDescending(d => d.IsInCharge).ThenBy(d => d.Name).ToList(), rows);
    }

    public async Task<NursingPatientDto> GetPatientAsync(Guid admissionId, CancellationToken cancellationToken) =>
        await support.GetPatientAsync(await support.GetStayAsync(admissionId, cancellationToken), cancellationToken);

    private static WardOptionDto ToOption(WardSummary w) => new(w.Id, w.Code, w.Name);
}
