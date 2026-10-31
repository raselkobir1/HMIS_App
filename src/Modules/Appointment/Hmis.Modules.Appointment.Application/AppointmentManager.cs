using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Appointment;
using Hmis.BuildingBlocks.Contracts.Billing;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Appointment.Domain.Entities;
using AppointmentEntity = Hmis.Modules.Appointment.Domain.Entities.Appointment;

namespace Hmis.Modules.Appointment.Application;

public sealed class AppointmentManager(
    IAppointmentRepository appointments,
    IScheduleRepository schedules,
    ILeaveRepository leaves,
    IAppointmentUnitOfWork unitOfWork,
    IPatientLookup patientLookup,
    IDoctorLookup doctorLookup,
    IUserEmployeeLookup userEmployeeLookup,
    INumberSequenceService numberSequences,
    IChargeService charges,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IAppointmentManager, IOpdEncounterService
{
    // ---------- Availability ----------

    public async Task<IReadOnlyList<SessionAvailabilityDto>> GetAvailabilityAsync(AvailabilityQuery query, CancellationToken cancellationToken)
    {
        var sessions = (await schedules.GetRunningOnAsync(query.Date, query.DoctorId, cancellationToken)).Where(s => s.RunsOn(query.Date)).ToList();
        var dayLeaves = await leaves.GetForDateAsync(query.Date, query.DoctorId, cancellationToken);
        var counts = await appointments.GetSessionCountsAsync(sessions.Select(s => s.Id).ToList(), query.Date, cancellationToken);
        var outOfWindow = query.Date < dateTime.LocalToday || query.Date > dateTime.LocalToday.AddDays(BookingWindow.MaxDaysAhead);

        return sessions.OrderBy(s => s.StartTime).Select(s =>
        {
            var count = counts.GetValueOrDefault(s.Id, new SessionCount(0, 0));
            var leave = dayLeaves.FirstOrDefault(l => l.Covers(s.Id));
            var hasEnded = HasEnded(s, query.Date);
            var isFull = count.Booked >= s.MaxPatients;
            var nextSerial = count.MaxSerial + 1;
            return new SessionAvailabilityDto(s.Id, s.StartTime, s.EndTime, s.Room, s.MaxPatients, count.Booked,
                Math.Max(0, s.MaxPatients - count.Booked), nextSerial, s.EstimatedTimeFor(nextSerial), isFull, hasEnded,
                leave is not null, leave?.Reason, !isFull && !hasEnded && leave is null && !outOfWindow);
        }).ToList();
    }

    // ---------- Queries ----------

    public async Task<PagedResponse<AppointmentDto>> SearchAsync(AppointmentListQuery query, CancellationToken cancellationToken)
    {
        var page = await appointments.SearchAsync(query, cancellationToken);
        var context = await LoadContextAsync(page.Items, cancellationToken);
        return page.Map(a => ToDto(a, context));
    }

    public async Task<AppointmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDtoAsync(await GetAsync(id, cancellationToken), cancellationToken);

    // ---------- Booking ----------

    public async Task<AppointmentDto> BookAsync(AppointmentBookDto request, CancellationToken cancellationToken)
    {
        var today = dateTime.LocalToday;
        if (request.Source == AppointmentSource.WalkIn && request.AppointmentDate != today)
        {
            throw new BusinessException(AppointmentErrorCodes.DateOutOfRange, "Walk-ins can only be booked for today.", "appointmentDate");
        }

        await EnsurePatientBookableAsync(request.PatientId, cancellationToken);
        var doctor = await GetBookableDoctorAsync(request.DoctorId, cancellationToken);
        var schedule = await GetBookableSessionAsync(request.ScheduleId, request.DoctorId, request.AppointmentDate, cancellationToken);

        // Allocated outside the booking transaction (Administration's own atomic statement); a failed booking leaves a gap.
        var number = await numberSequences.NextAsync(AppointmentSequences.AppointmentNumber, cancellationToken);
        var fee = request.VisitType == VisitType.New ? doctor.NewPatientFee : doctor.FollowUpFee;

        var appointment = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var serial = await ReserveSerialAsync(schedule, request.AppointmentDate, request.PatientId, null, ct);
            var booked = AppointmentEntity.Book(number, request.PatientId, request.DoctorId, schedule.Id, request.AppointmentDate,
                serial, schedule.EstimatedTimeFor(serial), request.VisitType, request.Source, fee, request.Notes);
            await appointments.AddAsync(booked, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return booked;
        }, cancellationToken);

        // A walk-in is already at the counter, so they join the queue immediately (and are billed like any check-in).
        if (request.Source == AppointmentSource.WalkIn)
        {
            await CheckInAndBillAsync(appointment, cancellationToken);
        }

        return await ToDtoAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentDto> RescheduleAsync(Guid id, AppointmentRescheduleDto request, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(id, cancellationToken);
        appointments.SetExpectedRowVersion(appointment, request.RowVersion);

        await GetBookableDoctorAsync(appointment.DoctorId, cancellationToken);
        var schedule = await GetBookableSessionAsync(request.ScheduleId, appointment.DoctorId, request.AppointmentDate, cancellationToken);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var serial = await ReserveSerialAsync(schedule, request.AppointmentDate, appointment.PatientId, appointment.Id, ct);
            appointment.Reschedule(schedule.Id, request.AppointmentDate, serial, schedule.EstimatedTimeFor(serial));
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await ToDtoAsync(appointment, cancellationToken);
    }

    // ---------- Status changes ----------

    public Task<AppointmentDto> ConfirmAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, a => a.Confirm(), cancellationToken);

    public async Task<AppointmentDto> CheckInAsync(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(id, cancellationToken);
        await CheckInAndBillAsync(appointment, cancellationToken);
        return await ToDtoAsync(appointment, cancellationToken);
    }

    /// <summary>
    /// Cancels the appointment and its OPD bill. Billing keeps a bill that was already paid, for the cashier to refund.
    /// The bill is cancelled before the appointment is saved; both steps are safe to repeat.
    /// </summary>
    public async Task<AppointmentDto> CancelAsync(Guid id, AppointmentCancelDto request, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(id, cancellationToken);
        var wasCheckedIn = appointment.Status == AppointmentStatus.Waiting;
        appointment.Cancel(request.Reason, dateTime.UtcNow);
        if (wasCheckedIn)
        {
            await charges.CancelAsync(ChargeSources.OpdAppointment, appointment.Id,
                $"Appointment {appointment.AppointmentNumber} cancelled: {appointment.CancellationReason}", cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(appointment, cancellationToken);
    }

    public Task<AppointmentDto> MarkNoShowAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, a => a.MarkNoShow(dateTime.LocalToday), cancellationToken);

    public async Task<AppointmentDto> StartConsultationAsync(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(id, cancellationToken);
        await EnsureOwnPatientAsync(appointment, cancellationToken);
        await StartAsync(appointment, cancellationToken);
        return await ToDtoAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentDto> CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(id, cancellationToken);
        await EnsureOwnPatientAsync(appointment, cancellationToken);
        appointment.Complete(dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(appointment, cancellationToken);
    }

    /// <summary>Starts the signed-in doctor's next waiting patient today (earliest session, lowest serial).</summary>
    public async Task<AppointmentDto> CallNextAsync(CancellationToken cancellationToken)
    {
        var doctorId = await GetCurrentDoctorIdAsync(cancellationToken);
        var today = await appointments.GetForDateAsync(dateTime.LocalToday, doctorId, cancellationToken);
        var sessions = (await schedules.GetByIdsAsync(today.Select(a => a.ScheduleId).Distinct().ToList(), cancellationToken)).ToDictionary(s => s.Id);

        var next = today
            .Where(a => a.Status == AppointmentStatus.Waiting)
            .OrderBy(a => sessions.GetValueOrDefault(a.ScheduleId)?.StartTime ?? TimeOnly.MaxValue)
            .ThenBy(a => a.SerialNumber)
            .FirstOrDefault()
            ?? throw new BusinessException(AppointmentErrorCodes.QueueEmpty, "No patients are waiting.");

        var tracked = await GetAsync(next.Id, cancellationToken);
        await StartAsync(tracked, cancellationToken);
        return await ToDtoAsync(tracked, cancellationToken);
    }

    // ---------- Queues ----------

    public async Task<MyQueueDto> GetMyQueueAsync(QueueQuery query, CancellationToken cancellationToken)
    {
        var doctorId = await GetCurrentDoctorIdAsync(cancellationToken);
        var date = query.Date ?? dateTime.LocalToday;
        var list = await appointments.GetForDateAsync(date, doctorId, cancellationToken);
        var context = await LoadContextAsync(list, cancellationToken);
        var dtos = list.Select(a => ToDto(a, context)).ToList();

        var ordered = dtos.OrderBy(a => context.Sessions.GetValueOrDefault(a.ScheduleId)?.StartTime ?? TimeOnly.MaxValue).ThenBy(a => a.SerialNumber).ToList();
        return new MyQueueDto(
            doctorId,
            context.Names.Doctors.GetValueOrDefault(doctorId)?.FullName ?? (await doctorLookup.GetAsync(doctorId, cancellationToken))?.FullName,
            date,
            ordered.FirstOrDefault(a => a.Status == AppointmentStatus.InConsultation),
            ordered.Where(a => a.Status == AppointmentStatus.Waiting).ToList(),
            ordered.Where(a => a.Status is not (AppointmentStatus.Waiting or AppointmentStatus.InConsultation)).ToList());
    }

    public async Task<QueueBoardDto> GetQueueBoardAsync(QueueQuery query, CancellationToken cancellationToken)
    {
        var date = query.Date ?? dateTime.LocalToday;
        var sessions = (await schedules.GetRunningOnAsync(date, null, cancellationToken)).Where(s => s.RunsOn(date)).ToList();
        var dayLeaves = await leaves.GetForDateAsync(date, null, cancellationToken);
        sessions = sessions.Where(s => !dayLeaves.Any(l => l.DoctorId == s.DoctorId && l.Covers(s.Id))).ToList();

        var list = await appointments.GetForDateAsync(date, null, cancellationToken);
        var doctors = (await doctorLookup.GetManyAsync(sessions.Select(s => s.DoctorId).Distinct().ToList(), cancellationToken)).ToDictionary(d => d.DoctorId);

        var board = sessions
            .OrderBy(s => s.StartTime).ThenBy(s => doctors.GetValueOrDefault(s.DoctorId)?.FullName)
            .Select(s =>
            {
                var inSession = list.Where(a => a.ScheduleId == s.Id).ToList();
                return new QueueSessionDto(
                    s.Id,
                    doctors.GetValueOrDefault(s.DoctorId)?.FullName,
                    doctors.GetValueOrDefault(s.DoctorId)?.Specialization,
                    s.Room,
                    s.StartTime,
                    s.EndTime,
                    inSession.FirstOrDefault(a => a.Status == AppointmentStatus.InConsultation)?.SerialNumber,
                    inSession.Where(a => a.Status == AppointmentStatus.Waiting).OrderBy(a => a.SerialNumber).Select(a => a.SerialNumber).ToList(),
                    inSession.Count(a => a.Status == AppointmentStatus.Completed));
            })
            .ToList();

        return new QueueBoardDto(date, dateTime.UtcNow, board);
    }

    // ---------- IOpdEncounterService (contract for Clinical; encounter id = appointment id) ----------

    async Task<OpdEncounter?> IOpdEncounterService.GetAsync(Guid encounterId, CancellationToken cancellationToken) =>
        await appointments.GetByIdAsync(encounterId, cancellationToken) is { } a ? ToEncounter(a) : null;

    async Task<OpdEncounter> IOpdEncounterService.StartAsync(Guid encounterId, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(encounterId, cancellationToken);
        await EnsureOwnPatientAsync(appointment, cancellationToken);
        if (appointment.Status != AppointmentStatus.InConsultation)
        {
            await StartAsync(appointment, cancellationToken);
        }

        return ToEncounter(appointment);
    }

    async Task<OpdEncounter> IOpdEncounterService.CompleteAsync(Guid encounterId, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(encounterId, cancellationToken);
        if (appointment.Status != AppointmentStatus.Completed)
        {
            await EnsureOwnPatientAsync(appointment, cancellationToken);
            appointment.Complete(dateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToEncounter(appointment);
    }

    Task<bool> IOpdEncounterService.HasRelationshipAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken) =>
        appointments.HasRelationshipAsync(doctorId, patientId, cancellationToken);

    private static OpdEncounter ToEncounter(AppointmentEntity a) =>
        new(a.Id, a.AppointmentNumber, a.PatientId, a.DoctorId, a.AppointmentDate, a.SerialNumber, a.VisitType.ToString(), a.Status.ToString());

    // ---------- helpers ----------

    /// <summary>
    /// Checks the patient in and posts the consultation fee to Billing (Q-BILL-2). The charge is posted before the
    /// check-in is saved: posting is idempotent, so if either step fails the patient is simply not checked in yet and
    /// the receptionist can retry. Payment is never required before the consultation.
    /// </summary>
    private async Task CheckInAndBillAsync(AppointmentEntity appointment, CancellationToken cancellationToken)
    {
        appointment.CheckIn(dateTime.LocalToday, dateTime.UtcNow);
        if (appointment.Fee > 0)
        {
            var doctor = await doctorLookup.GetAsync(appointment.DoctorId, cancellationToken);
            var visit = appointment.VisitType == VisitType.New ? "new patient" : "follow-up";
            await charges.PostAsync(new ChargeRequest(
                ChargeSources.OpdAppointment,
                appointment.Id,
                appointment.AppointmentNumber,
                appointment.PatientId,
                [new ChargeLine($"Consultation fee — {doctor?.FullName ?? "Doctor"} ({visit})", ChargeCategories.Consultation, 1, appointment.Fee)]),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AppointmentDto> ChangeAsync(Guid id, Action<AppointmentEntity> change, CancellationToken cancellationToken)
    {
        var appointment = await GetAsync(id, cancellationToken);
        change(appointment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(appointment, cancellationToken);
    }

    private async Task StartAsync(AppointmentEntity appointment, CancellationToken cancellationToken)
    {
        if (await appointments.HasConsultationInProgressAsync(appointment.DoctorId, appointment.Id, cancellationToken))
        {
            throw new BusinessException(AppointmentErrorCodes.ConsultationInProgress, "Complete the current consultation before calling the next patient.");
        }

        appointment.StartConsultation(dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Capacity check + next serial under the session-day lock (call inside a transaction).</summary>
    private async Task<int> ReserveSerialAsync(DoctorSchedule schedule, DateOnly date, Guid patientId, Guid? excludeAppointmentId, CancellationToken cancellationToken)
    {
        await appointments.LockSessionDayAsync(schedule.Id, date, cancellationToken);

        if (await appointments.PatientHasActiveWithDoctorAsync(patientId, schedule.DoctorId, date, excludeAppointmentId, cancellationToken))
        {
            throw new ConflictException("This patient already has an appointment with this doctor on that date.", AppointmentErrorCodes.AlreadyBooked);
        }

        var count = await appointments.GetSessionCountAsync(schedule.Id, date, cancellationToken);
        if (count.Booked >= schedule.MaxPatients)
        {
            throw new BusinessException(AppointmentErrorCodes.SessionFull, $"This session is full ({schedule.MaxPatients} patients).");
        }

        // Serial numbers are never reused within a session day, even after cancellations (tokens stay unique).
        return count.MaxSerial + 1;
    }

    private async Task EnsurePatientBookableAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var patient = await patientLookup.GetAsync(patientId, cancellationToken);
        if (patient is null)
        {
            throw new BusinessException(AppointmentErrorCodes.PatientUnavailable, "Select a registered patient.", "patientId");
        }

        if (patient.Status != "Active")
        {
            throw new BusinessException(AppointmentErrorCodes.PatientUnavailable,
                patient.Status == "Merged"
                    ? "This patient record was merged into another record. Book with the surviving record."
                    : "This patient record is inactive.",
                "patientId");
        }
    }

    private async Task<DoctorSummary> GetBookableDoctorAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var doctor = await doctorLookup.GetAsync(doctorId, cancellationToken);
        return doctor is { IsCurrent: true, IsAcceptingAppointments: true }
            ? doctor
            : throw new BusinessException(AppointmentErrorCodes.DoctorUnavailable, "This doctor is not accepting appointments.", "doctorId");
    }

    private async Task<DoctorSchedule> GetBookableSessionAsync(Guid scheduleId, Guid doctorId, DateOnly date, CancellationToken cancellationToken)
    {
        var today = dateTime.LocalToday;
        if (date < today || date > today.AddDays(BookingWindow.MaxDaysAhead))
        {
            throw new BusinessException(AppointmentErrorCodes.DateOutOfRange,
                $"Appointments can be booked from today up to {BookingWindow.MaxDaysAhead} days ahead.", "appointmentDate");
        }

        var schedule = await schedules.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null || schedule.DoctorId != doctorId || !schedule.RunsOn(date))
        {
            throw new BusinessException(AppointmentErrorCodes.SessionInvalid, "The doctor has no such session on that date.", "scheduleId");
        }

        if (HasEnded(schedule, date))
        {
            throw new BusinessException(AppointmentErrorCodes.SessionEnded, "This session has already ended today.", "scheduleId");
        }

        var leave = (await leaves.GetForDateAsync(date, doctorId, cancellationToken)).FirstOrDefault(l => l.Covers(scheduleId));
        if (leave is not null)
        {
            throw new BusinessException(AppointmentErrorCodes.DoctorOnLeave, $"The doctor is on leave: {leave.Reason}");
        }

        return schedule;
    }

    private bool HasEnded(DoctorSchedule schedule, DateOnly date) =>
        date == dateTime.LocalToday && TimeOnly.FromDateTime(dateTime.LocalNow) >= schedule.EndTime;

    /// <summary>Doctors may only run their own queue: the signed-in login must be linked to the appointment's doctor.</summary>
    private async Task EnsureOwnPatientAsync(AppointmentEntity appointment, CancellationToken cancellationToken)
    {
        if (await GetCurrentDoctorIdAsync(cancellationToken) != appointment.DoctorId)
        {
            throw new ForbiddenException("Only the patient's own doctor can start or complete this consultation.");
        }
    }

    private async Task<Guid> GetCurrentDoctorIdAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var employeeId = await userEmployeeLookup.GetEmployeeIdAsync(userId, cancellationToken);
        if (employeeId is null || await doctorLookup.GetAsync(employeeId.Value, cancellationToken) is null)
        {
            throw new BusinessException(AppointmentErrorCodes.NotYourPatient,
                "Your login is not linked to a doctor profile. Ask an administrator to link it to your employee record.");
        }

        return employeeId.Value;
    }

    private async Task<AppointmentEntity> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await appointments.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Appointment", id);

    private sealed record ListContext(NameMaps Names, IReadOnlyDictionary<Guid, DoctorSchedule> Sessions);

    private async Task<ListContext> LoadContextAsync(IEnumerable<AppointmentEntity> list, CancellationToken cancellationToken)
    {
        var items = list.ToList();
        var patients = await patientLookup.GetManyAsync(items.Select(a => a.PatientId).Distinct().ToList(), cancellationToken);
        var doctors = await doctorLookup.GetManyAsync(items.Select(a => a.DoctorId).Distinct().ToList(), cancellationToken);
        var sessions = await schedules.GetByIdsAsync(items.Select(a => a.ScheduleId).Distinct().ToList(), cancellationToken);
        return new ListContext(
            new NameMaps(patients.ToDictionary(p => p.Id), doctors.ToDictionary(d => d.DoctorId)),
            sessions.ToDictionary(s => s.Id));
    }

    private async Task<AppointmentDto> ToDtoAsync(AppointmentEntity a, CancellationToken cancellationToken) =>
        ToDto(a, await LoadContextAsync([a], cancellationToken));

    private static AppointmentDto ToDto(AppointmentEntity a, ListContext context)
    {
        var patient = context.Names.Patients.GetValueOrDefault(a.PatientId);
        return new AppointmentDto(
            a.Id, a.AppointmentNumber, a.PatientId, patient?.MRNumber, patient?.FullName, patient?.Gender, patient?.DateOfBirth,
            a.DoctorId, context.Names.Doctors.GetValueOrDefault(a.DoctorId)?.FullName, a.ScheduleId,
            context.Sessions.GetValueOrDefault(a.ScheduleId)?.Room, a.AppointmentDate, a.SerialNumber, a.EstimatedTime,
            a.VisitType, a.Source, a.Fee, a.Notes, a.Status, a.CheckedInAt, a.ConsultationStartedAt, a.CompletedAt,
            a.CancellationReason, a.RowVersion);
    }
}
