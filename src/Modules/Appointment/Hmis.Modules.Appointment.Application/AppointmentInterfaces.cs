using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Appointment.Domain.Entities;
using AppointmentEntity = Hmis.Modules.Appointment.Domain.Entities.Appointment;

namespace Hmis.Modules.Appointment.Application;

public interface IAppointmentUnitOfWork : IUnitOfWork;

public sealed record SessionCount(int Booked, int MaxSerial);

public sealed record FutureUsage(int Count, int MaxPerDay, DateOnly? LastDate);

public interface IScheduleRepository : IRepository<DoctorSchedule>, IConcurrencyRepository<DoctorSchedule>
{
    Task<IReadOnlyList<DoctorSchedule>> SearchAsync(ScheduleQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorSchedule>> GetActiveForDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    /// <summary>Active sessions for this weekday whose effective period includes the date.</summary>
    Task<IReadOnlyList<DoctorSchedule>> GetRunningOnAsync(DateOnly date, Guid? doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorSchedule>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Serializes session changes for one doctor (PostgreSQL transaction-scoped advisory lock), so overlap checks
    /// cannot race. Must be called inside a transaction.
    /// </summary>
    Task LockDoctorAsync(Guid doctorId, CancellationToken cancellationToken);
}

public interface ILeaveRepository : IRepository<DoctorLeave>
{
    Task<IReadOnlyList<DoctorLeave>> GetForDateAsync(DateOnly date, Guid? doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorLeave>> SearchAsync(LeaveQuery query, CancellationToken cancellationToken);
}

public interface IAppointmentRepository : IRepository<AppointmentEntity>, IConcurrencyRepository<AppointmentEntity>
{
    /// <summary>
    /// Serializes bookings for one doctor on one date (PostgreSQL transaction-scoped advisory lock), so capacity
    /// checks, serial numbers and the one-appointment-per-doctor-per-day rule cannot race across the doctor's
    /// sessions that day. Must be called inside a transaction.
    /// </summary>
    Task LockDoctorDayAsync(Guid doctorId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>Booked count (without <paramref name="excludeId"/>, e.g. the appointment being rescheduled) and highest serial of a session day.</summary>
    Task<SessionCount> GetSessionCountAsync(Guid scheduleId, DateOnly date, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, SessionCount>> GetSessionCountsAsync(IReadOnlyCollection<Guid> scheduleIds, DateOnly date, CancellationToken cancellationToken);

    Task<bool> PatientHasActiveWithDoctorAsync(Guid patientId, Guid doctorId, DateOnly date, Guid? excludeId, CancellationToken cancellationToken);

    Task<PagedResponse<AppointmentEntity>> SearchAsync(AppointmentListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppointmentEntity>> GetForDateAsync(DateOnly date, Guid? doctorId, CancellationToken cancellationToken);

    /// <summary>True when the doctor has another appointment in consultation on <paramref name="date"/>.</summary>
    Task<bool> HasConsultationInProgressAsync(Guid doctorId, DateOnly date, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>The doctor's appointments still in consultation from days before <paramref name="before"/> (tracked, for closing).</summary>
    Task<IReadOnlyList<AppointmentEntity>> GetStaleConsultationsAsync(Guid doctorId, DateOnly before, CancellationToken cancellationToken);

    Task<FutureUsage> GetFutureUsageAsync(Guid scheduleId, DateOnly fromDate, CancellationToken cancellationToken);

    /// <summary>True when the doctor has any non-cancelled appointment with the patient (clinical access rule).</summary>
    Task<bool> HasRelationshipAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken);

    /// <summary>Appointments today or later that are not finished (scheduled, confirmed, waiting, in consultation).</summary>
    Task<int> CountOpenForPatientAsync(Guid patientId, DateOnly fromDate, CancellationToken cancellationToken);

    /// <summary>Appointments not yet seen (Scheduled/Confirmed/Waiting) that a leave would affect.</summary>
    Task<int> CountPendingAsync(Guid doctorId, DateOnly date, Guid? scheduleId, CancellationToken cancellationToken);
}

public interface IScheduleManager
{
    Task<IReadOnlyList<ScheduleDto>> SearchAsync(ScheduleQuery query, CancellationToken cancellationToken);

    Task<ScheduleDto> CreateAsync(ScheduleCreateDto request, CancellationToken cancellationToken);

    Task<ScheduleDto> UpdateAsync(Guid id, ScheduleUpdateDto request, CancellationToken cancellationToken);

    Task<ScheduleDto> SetStatusAsync(Guid id, ScheduleStatusDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeaveDto>> SearchLeavesAsync(LeaveQuery query, CancellationToken cancellationToken);

    Task<LeaveDto> AddLeaveAsync(LeaveCreateDto request, CancellationToken cancellationToken);

    Task RemoveLeaveAsync(Guid id, CancellationToken cancellationToken);
}

public interface IAppointmentManager
{
    Task<AppointmentDaySummaryDto> GetDaySummaryAsync(DateOnly? date, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionAvailabilityDto>> GetAvailabilityAsync(AvailabilityQuery query, CancellationToken cancellationToken);

    Task<PagedResponse<AppointmentDto>> SearchAsync(AppointmentListQuery query, CancellationToken cancellationToken);

    Task<AppointmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AppointmentDto> BookAsync(AppointmentBookDto request, CancellationToken cancellationToken);

    Task<AppointmentDto> RescheduleAsync(Guid id, AppointmentRescheduleDto request, CancellationToken cancellationToken);

    Task<AppointmentDto> ConfirmAsync(Guid id, CancellationToken cancellationToken);

    Task<AppointmentDto> CheckInAsync(Guid id, CancellationToken cancellationToken);

    Task<AppointmentDto> CancelAsync(Guid id, AppointmentCancelDto request, CancellationToken cancellationToken);

    Task<AppointmentDto> MarkNoShowAsync(Guid id, CancellationToken cancellationToken);

    Task<AppointmentDto> StartConsultationAsync(Guid id, CancellationToken cancellationToken);

    Task<AppointmentDto> CompleteAsync(Guid id, CancellationToken cancellationToken);

    Task<AppointmentDto> CallNextAsync(CancellationToken cancellationToken);

    Task<MyQueueDto> GetMyQueueAsync(QueueQuery query, CancellationToken cancellationToken);

    Task<QueueBoardDto> GetQueueBoardAsync(QueueQuery query, CancellationToken cancellationToken);
}
