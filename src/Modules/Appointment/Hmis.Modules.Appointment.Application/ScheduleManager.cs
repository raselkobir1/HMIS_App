using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.Modules.Appointment.Domain.Entities;

namespace Hmis.Modules.Appointment.Application;

/// <summary>Doctor weekly sessions and leave days.</summary>
public sealed class ScheduleManager(
    IScheduleRepository schedules,
    ILeaveRepository leaves,
    IAppointmentRepository appointments,
    IAppointmentUnitOfWork unitOfWork,
    IDoctorLookup doctorLookup,
    IDateTimeProvider dateTime) : IScheduleManager
{
    public async Task<IReadOnlyList<ScheduleDto>> SearchAsync(ScheduleQuery query, CancellationToken cancellationToken)
    {
        var list = await schedules.SearchAsync(query, cancellationToken);
        var doctors = await DoctorsAsync(list.Select(s => s.DoctorId), cancellationToken);
        return list.Select(s => ToDto(s, doctors)).ToList();
    }

    public async Task<ScheduleDto> CreateAsync(ScheduleCreateDto request, CancellationToken cancellationToken)
    {
        var doctor = await doctorLookup.GetAsync(request.DoctorId, cancellationToken);
        if (doctor is not { IsCurrent: true })
        {
            throw new BusinessException(AppointmentErrorCodes.DoctorUnavailable, "Select a current doctor with a doctor profile.", "doctorId");
        }

        var schedule = DoctorSchedule.Create(request.DoctorId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.MaxPatients, request.AverageMinutesPerPatient, request.Room, request.EffectiveFrom, request.EffectiveTo);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await EnsureNoOverlapAsync(schedule, ct);
            await schedules.AddAsync(schedule, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return ToDto(schedule, new Dictionary<Guid, DoctorSummary> { [doctor.DoctorId] = doctor });
    }

    public async Task<ScheduleDto> UpdateAsync(Guid id, ScheduleUpdateDto request, CancellationToken cancellationToken)
    {
        var schedule = await GetAsync(id, cancellationToken);
        schedules.SetExpectedRowVersion(schedule, request.RowVersion);

        // Upcoming bookings pin the session's day and hours; capacity and end date must still fit them.
        var usage = await appointments.GetFutureUsageAsync(id, dateTime.LocalToday, cancellationToken);
        if (usage.Count > 0)
        {
            if (request.DayOfWeek != schedule.DayOfWeek || request.StartTime != schedule.StartTime ||
                request.EndTime != schedule.EndTime || request.EffectiveFrom != schedule.EffectiveFrom)
            {
                throw new BusinessException(AppointmentErrorCodes.ScheduleInUse,
                    $"{usage.Count} upcoming appointment(s) use this session. Day, hours and start date cannot change; create a new session instead.");
            }

            if (request.MaxPatients < usage.MaxPerDay)
            {
                throw new BusinessException(AppointmentErrorCodes.ScheduleInUse,
                    $"An upcoming date already has {usage.MaxPerDay} booking(s); maximum patients cannot be lower.", "maxPatients");
            }

            if (request.EffectiveTo.HasValue && usage.LastDate.HasValue && request.EffectiveTo.Value < usage.LastDate.Value)
            {
                throw new BusinessException(AppointmentErrorCodes.ScheduleInUse,
                    $"Appointments are booked until {usage.LastDate:yyyy-MM-dd}; the end date cannot be earlier.", "effectiveTo");
            }
        }

        schedule.Update(request.DayOfWeek, request.StartTime, request.EndTime, request.MaxPatients,
            request.AverageMinutesPerPatient, request.Room, request.EffectiveFrom, request.EffectiveTo);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await EnsureNoOverlapAsync(schedule, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return ToDto(schedule, await DoctorsAsync([schedule.DoctorId], cancellationToken));
    }

    public async Task<ScheduleDto> SetStatusAsync(Guid id, ScheduleStatusDto request, CancellationToken cancellationToken)
    {
        var schedule = await GetAsync(id, cancellationToken);
        if (request.IsActive)
        {
            schedule.Activate();
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await EnsureNoOverlapAsync(schedule, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        else
        {
            var usage = await appointments.GetFutureUsageAsync(id, dateTime.LocalToday, cancellationToken);
            if (usage.Count > 0)
            {
                throw new BusinessException(AppointmentErrorCodes.ScheduleInUse,
                    $"{usage.Count} upcoming appointment(s) use this session. Reschedule or cancel them first.");
            }

            schedule.Deactivate();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToDto(schedule, await DoctorsAsync([schedule.DoctorId], cancellationToken));
    }

    public async Task<IReadOnlyList<LeaveDto>> SearchLeavesAsync(LeaveQuery query, CancellationToken cancellationToken)
    {
        var list = await leaves.SearchAsync(query, cancellationToken);
        var doctors = await DoctorsAsync(list.Select(l => l.DoctorId), cancellationToken);
        var sessions = (await schedules.GetByIdsAsync(list.Where(l => l.ScheduleId.HasValue).Select(l => l.ScheduleId!.Value).Distinct().ToList(), cancellationToken))
            .ToDictionary(s => s.Id);
        return list.Select(l => ToDto(l, doctors, sessions)).ToList();
    }

    public async Task<LeaveDto> AddLeaveAsync(LeaveCreateDto request, CancellationToken cancellationToken)
    {
        if (request.Date < dateTime.LocalToday)
        {
            throw new BusinessException(AppointmentErrorCodes.DateOutOfRange, "Leave cannot be recorded for a past date.", "date");
        }

        var doctor = await doctorLookup.GetAsync(request.DoctorId, cancellationToken)
            ?? throw new BusinessException(AppointmentErrorCodes.DoctorUnavailable, "Select a doctor.", "doctorId");

        Dictionary<Guid, DoctorSchedule> sessions = [];
        if (request.ScheduleId is { } scheduleId)
        {
            var schedule = await GetAsync(scheduleId, cancellationToken);
            if (schedule.DoctorId != request.DoctorId || !schedule.RunsOn(request.Date))
            {
                throw new BusinessException(AppointmentErrorCodes.SessionInvalid, "This session does not run on that date.", "scheduleId");
            }

            sessions[schedule.Id] = schedule;
        }

        var existing = await leaves.GetForDateAsync(request.Date, request.DoctorId, cancellationToken);
        if (existing.Any(l => l.ScheduleId is null || l.ScheduleId == request.ScheduleId))
        {
            throw new ConflictException("Leave is already recorded for this doctor at that time.", AppointmentErrorCodes.LeaveExists);
        }

        // Booked patients must be moved first, so nobody arrives for a cancelled session unaware.
        var pending = await appointments.CountPendingAsync(request.DoctorId, request.Date, request.ScheduleId, cancellationToken);
        if (pending > 0)
        {
            throw new BusinessException(AppointmentErrorCodes.LeaveConflict,
                $"{pending} appointment(s) are booked for this time. Reschedule or cancel them before recording leave.");
        }

        var leave = DoctorLeave.Create(request.DoctorId, request.Date, request.ScheduleId, request.Reason);
        await leaves.AddAsync(leave, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(leave, new Dictionary<Guid, DoctorSummary> { [doctor.DoctorId] = doctor }, sessions);
    }

    public async Task RemoveLeaveAsync(Guid id, CancellationToken cancellationToken)
    {
        var leave = await leaves.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Leave", id);
        leaves.Remove(leave);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<DoctorSchedule> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await schedules.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Session", id);

    /// <summary>
    /// Overlap check under the per-doctor lock (call inside the transaction that saves the session), so two
    /// concurrent saves for the same doctor cannot both pass the read-then-write check.
    /// </summary>
    private async Task EnsureNoOverlapAsync(DoctorSchedule schedule, CancellationToken cancellationToken)
    {
        await schedules.LockDoctorAsync(schedule.DoctorId, cancellationToken);
        var existing = await schedules.GetActiveForDoctorAsync(schedule.DoctorId, cancellationToken);
        if (existing.FirstOrDefault(schedule.OverlapsWith) is { } clash)
        {
            throw new BusinessException(AppointmentErrorCodes.ScheduleOverlap,
                $"This overlaps the doctor's {clash.DayOfWeek} {clash.StartTime:HH\\:mm}–{clash.EndTime:HH\\:mm} session.");
        }
    }

    private async Task<Dictionary<Guid, DoctorSummary>> DoctorsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken) =>
        (await doctorLookup.GetManyAsync(ids.Distinct().ToList(), cancellationToken)).ToDictionary(d => d.DoctorId);

    private static ScheduleDto ToDto(DoctorSchedule s, IReadOnlyDictionary<Guid, DoctorSummary> doctors)
    {
        var doctor = doctors.GetValueOrDefault(s.DoctorId);
        return new ScheduleDto(s.Id, s.DoctorId, doctor?.FullName, doctor?.Specialization, s.DayOfWeek, s.StartTime, s.EndTime,
            s.MaxPatients, s.AverageMinutesPerPatient, s.Room, s.EffectiveFrom, s.EffectiveTo, s.IsActive, s.RowVersion);
    }

    private static LeaveDto ToDto(DoctorLeave l, IReadOnlyDictionary<Guid, DoctorSummary> doctors, IReadOnlyDictionary<Guid, DoctorSchedule> sessions)
    {
        var session = l.ScheduleId is { } id ? sessions.GetValueOrDefault(id) : null;
        var label = session is null ? null : $"{session.StartTime:HH\\:mm}–{session.EndTime:HH\\:mm}{(session.Room is null ? "" : " · " + session.Room)}";
        return new LeaveDto(l.Id, l.DoctorId, doctors.GetValueOrDefault(l.DoctorId)?.FullName, l.Date, l.ScheduleId, label, l.Reason);
    }
}
