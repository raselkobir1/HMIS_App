using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Ipd;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Nursing.Domain.Entities;

namespace Hmis.Modules.Nursing.Application;

/// <summary>Shift handovers per ward and the nurse roster (ward × shift).</summary>
public sealed class ShiftManager(
    IShiftHandoverRepository handovers,
    INurseAssignmentRepository assignments,
    INursingUnitOfWork unitOfWork,
    IInpatientLookup inpatients,
    IPatientLookup patientLookup,
    IEmployeeLookup employeeLookup,
    IPermissionChecker permissions,
    NursingSupport support,
    IDateTimeProvider dateTime) : IShiftManager
{
    private const int MaxRosterDays = 31;

    /// <summary>Double shifts happen; a third shift in one day does not.</summary>
    private const int MaxShiftsPerDay = 2;

    // ---------- Handover ----------

    public async Task<IReadOnlyList<HandoverDto>> GetHandoversAsync(HandoverQuery query, CancellationToken cancellationToken)
    {
        var ward = await GetWardAsync(query.WardId, cancellationToken);
        var date = query.Date ?? dateTime.LocalToday;
        var list = await handovers.GetForWardAsync(ward.Id, date.AddDays(-1), date, cancellationToken);
        return await ToDtosAsync(list, ward, cancellationToken);
    }

    /// <summary>Written for the current or the previous day's shifts; one handover per ward and shift.</summary>
    public async Task<HandoverDto> SaveHandoverAsync(HandoverSaveDto request, CancellationToken cancellationToken)
    {
        var ward = await GetWardAsync(request.WardId, cancellationToken);
        var today = dateTime.LocalToday;
        if (request.Date > today || request.Date < today.AddDays(-1))
        {
            throw new BusinessException(NursingErrorCodes.HandoverDate, "A handover can be written for today's or yesterday's shifts only.", "date");
        }

        // The shift must have started: the outgoing nurse hands over the current or the just-ended shift, not a later one.
        var now = dateTime.UtcNow;
        if (NursingClock.ShiftWindow(request.Date, request.Shift, dateTime).StartUtc > now)
        {
            throw new BusinessException(NursingErrorCodes.HandoverShift, "A handover is written for the current or the just-ended shift, not a shift that has not started.", "shift");
        }

        // Every note is about a patient still in this ward.
        var inWard = (await inpatients.GetActiveAsync(ward.Id, cancellationToken)).Select(s => s.AdmissionId).ToHashSet();
        foreach (var admissionId in request.Items.Select(i => i.AdmissionId).Distinct())
        {
            if (!inWard.Contains(admissionId))
            {
                var stay = await inpatients.GetAsync(admissionId, cancellationToken);
                throw new BusinessException(NursingErrorCodes.HandoverPatient, stay switch
                {
                    null => "A handover note refers to an unknown admission.",
                    { IsActive: false } => $"Admission {stay.AdmissionNumber} has been discharged and cannot be in the handover.",
                    _ => $"Admission {stay.AdmissionNumber} is in {stay.WardName ?? "another ward"}, not in {ward.Name}."
                }, "items");
            }
        }

        var items = request.Items.Select(i => new HandoverItemValues(i.AdmissionId, i.Note)).ToList();
        var handover = await handovers.FindAsync(ward.Id, request.Date, request.Shift, cancellationToken);
        if (handover is null)
        {
            handover = ShiftHandover.Create(ward.Id, request.Date, request.Shift, request.Summary, items);
            await handovers.AddAsync(handover, cancellationToken);
        }
        else
        {
            handovers.SetExpectedRowVersion(handover, request.RowVersion);
            handover.Update(support.UserId, request.Summary, items);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([handover], ward, cancellationToken))[0];
    }

    public async Task<HandoverDto> ReceiveHandoverAsync(Guid id, HandoverReceiveDto request, CancellationToken cancellationToken)
    {
        var handover = await handovers.GetWithItemsAsync(id, cancellationToken) ?? throw new NotFoundException("Handover", id);
        handovers.SetExpectedRowVersion(handover, request.RowVersion);
        handover.Receive(support.UserId, dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([handover], await GetWardAsync(handover.WardId, cancellationToken), cancellationToken))[0];
    }

    // ---------- Roster ----------

    public async Task<IReadOnlyList<RosterEntryDto>> GetRosterAsync(RosterQuery query, CancellationToken cancellationToken)
    {
        var userId = support.UserId;
        if (!await permissions.HasPermissionAsync(userId, NursingPermissions.View, cancellationToken)
            && !await permissions.HasPermissionAsync(userId, NursingPermissions.RosterManage, cancellationToken))
        {
            throw new ForbiddenException();
        }

        var from = query.From ?? dateTime.LocalToday;
        var to = query.To ?? from.AddDays(6);
        if (to < from || to.DayNumber - from.DayNumber >= MaxRosterDays)
        {
            throw new BusinessException("ROSTER_RANGE", $"Choose a range of at most {MaxRosterDays} days.", "to");
        }

        var list = await assignments.SearchAsync(from, to, query.WardId, cancellationToken);
        return await ToDtosAsync(list, cancellationToken);
    }

    public async Task<RosterEntryDto> AddRosterEntryAsync(RosterCreateDto request, CancellationToken cancellationToken)
    {
        var ward = await GetWardAsync(request.WardId, cancellationToken);
        if (!ward.IsActive)
        {
            throw new BusinessException(NursingErrorCodes.WardInvalid, "This ward is closed.", "wardId");
        }

        if (request.Date < dateTime.LocalToday)
        {
            throw new BusinessException(NursingErrorCodes.RosterPast, "Past shifts cannot be changed.", "date");
        }

        if (await employeeLookup.GetAsync(request.EmployeeId, cancellationToken) is not { IsCurrent: true })
        {
            throw new BusinessException(NursingErrorCodes.EmployeeInvalid, "Select a current employee.", "employeeId");
        }

        // The whole day across wards: a nurse works one ward per shift and at most two shifts (a double) in a day.
        var day = await assignments.SearchAsync(request.Date, request.Date, null, cancellationToken);
        var shift = day.Where(a => a.WardId == ward.Id && a.Shift == request.Shift).ToList();
        if (shift.Any(a => a.EmployeeId == request.EmployeeId))
        {
            throw new ConflictException("This nurse is already on this shift in this ward.", NursingErrorCodes.RosterDuplicate);
        }

        var mine = day.Where(a => a.EmployeeId == request.EmployeeId).ToList();
        if (mine.FirstOrDefault(a => a.Shift == request.Shift) is { } elsewhere)
        {
            var wardName = (await inpatients.GetWardsAsync(cancellationToken)).FirstOrDefault(w => w.Id == elsewhere.WardId)?.Name ?? "another ward";
            throw new ConflictException($"This nurse is already rostered to {wardName} for the {request.Shift.ToString().ToLowerInvariant()} shift on this date.",
                NursingErrorCodes.RosterConflict);
        }

        if (mine.Count >= MaxShiftsPerDay)
        {
            throw new BusinessException(NursingErrorCodes.RosterMaxShifts, $"A nurse can work at most {MaxShiftsPerDay} shifts in one day.", "shift");
        }

        if (request.IsInCharge && shift.Any(a => a.IsInCharge))
        {
            throw new BusinessException(NursingErrorCodes.RosterInCharge, "This shift already has a nurse in charge.", "isInCharge");
        }

        var assignment = NurseAssignment.Create(ward.Id, request.Date, request.Shift, request.EmployeeId, request.IsInCharge);
        await assignments.AddAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([assignment], cancellationToken))[0];
    }

    public async Task RemoveRosterEntryAsync(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await assignments.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Roster entry", id);
        if (assignment.Date < dateTime.LocalToday)
        {
            throw new BusinessException(NursingErrorCodes.RosterPast, "Past shifts cannot be changed.");
        }

        assignments.Remove(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------- helpers ----------

    private async Task<WardSummary> GetWardAsync(Guid wardId, CancellationToken cancellationToken) =>
        (await inpatients.GetWardsAsync(cancellationToken)).FirstOrDefault(w => w.Id == wardId)
        ?? throw new BusinessException(NursingErrorCodes.WardInvalid, "Select a ward.", "wardId");

    private async Task<IReadOnlyList<HandoverDto>> ToDtosAsync(IReadOnlyList<ShiftHandover> list, WardSummary ward, CancellationToken cancellationToken)
    {
        var stays = new Dictionary<Guid, InpatientStay>();
        foreach (var admissionId in list.SelectMany(h => h.Items.Select(i => i.AdmissionId)).Distinct())
        {
            if (await inpatients.GetAsync(admissionId, cancellationToken) is { } stay)
            {
                stays[admissionId] = stay;
            }
        }

        var patients = (await patientLookup.GetManyAsync(stays.Values.Select(s => s.PatientId).Distinct().ToList(), cancellationToken))
            .ToDictionary(p => p.Id);
        var names = await support.GetUserNamesAsync(list.SelectMany(h => new[] { h.CreatedBy, h.ReceivedBy }), cancellationToken);
        var userId = support.UserId;

        return list.OrderByDescending(h => h.Date).ThenByDescending(h => h.Shift).Select(h => new HandoverDto(
            h.Id, h.WardId, ward.Name, h.Date, h.Shift, h.Summary,
            h.Items.OrderBy(i => i.SortOrder).Select(i =>
            {
                var stay = stays.GetValueOrDefault(i.AdmissionId);
                var patient = stay is null ? null : patients.GetValueOrDefault(stay.PatientId);
                return new HandoverItemDto(i.AdmissionId, patient?.FullName, patient?.MRNumber, stay?.BedNumber, i.Note);
            }).ToList(),
            NursingSupport.NameOf(h.CreatedBy, names), h.CreatedAt, NursingSupport.NameOf(h.ReceivedBy, names), h.ReceivedAt, h.CreatedBy == userId,
            h.RowVersion)).ToList();
    }

    private async Task<IReadOnlyList<RosterEntryDto>> ToDtosAsync(IReadOnlyList<NurseAssignment> list, CancellationToken cancellationToken)
    {
        var wards = (await inpatients.GetWardsAsync(cancellationToken)).ToDictionary(w => w.Id);
        var employees = new Dictionary<Guid, EmployeeSummary?>();
        foreach (var id in list.Select(a => a.EmployeeId).Distinct())
        {
            employees[id] = await employeeLookup.GetAsync(id, cancellationToken);
        }

        return list.Select(a => new RosterEntryDto(a.Id, a.WardId, wards.GetValueOrDefault(a.WardId)?.Name, a.Date, a.Shift, a.EmployeeId,
            employees.GetValueOrDefault(a.EmployeeId)?.FullName, employees.GetValueOrDefault(a.EmployeeId)?.EmployeeCode, a.IsInCharge)).ToList();
    }
}
