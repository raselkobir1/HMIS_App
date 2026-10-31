using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Appointment.Domain.Entities;

/// <summary>
/// A weekly chamber session of a doctor (e.g. Sunday 17:00–21:00, Room 204, max 30 patients).
/// Patients get serial numbers within a session; the estimated time is start + (serial − 1) × average minutes.
/// </summary>
[Audited]
public sealed class DoctorSchedule : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    public const int MaxPatientsLimit = 200;

    private DoctorSchedule() { }

    /// <summary>The doctor's HR employee id.</summary>
    public Guid DoctorId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int MaxPatients { get; private set; }
    public int AverageMinutesPerPatient { get; private set; }
    public string? Room { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint RowVersion { get; private set; }

    public static DoctorSchedule Create(
        Guid doctorId, DayOfWeek day, TimeOnly start, TimeOnly end, int maxPatients, int averageMinutes,
        string? room, DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        var schedule = new DoctorSchedule { DoctorId = doctorId };
        schedule.Update(day, start, end, maxPatients, averageMinutes, room, effectiveFrom, effectiveTo);
        return schedule;
    }

    public void Update(
        DayOfWeek day, TimeOnly start, TimeOnly end, int maxPatients, int averageMinutes,
        string? room, DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (end <= start)
        {
            throw new DomainException("SCHEDULE_TIME_RANGE", "The session must end after it starts.");
        }

        if (maxPatients is < 1 or > MaxPatientsLimit)
        {
            throw new DomainException("SCHEDULE_CAPACITY", $"Maximum patients must be between 1 and {MaxPatientsLimit}.");
        }

        if (averageMinutes is < 1 or > 180)
        {
            throw new DomainException("SCHEDULE_AVERAGE_MINUTES", "Average minutes per patient must be between 1 and 180.");
        }

        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
        {
            throw new DomainException("SCHEDULE_EFFECTIVE_RANGE", "The end date cannot be before the start date.");
        }

        DayOfWeek = day;
        StartTime = start;
        EndTime = end;
        MaxPatients = maxPatients;
        AverageMinutesPerPatient = averageMinutes;
        Room = string.IsNullOrWhiteSpace(room) ? null : room.Trim();
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>True when this session runs on <paramref name="date"/>.</summary>
    public bool RunsOn(DateOnly date) =>
        IsActive && date.DayOfWeek == DayOfWeek && date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo.Value);

    /// <summary>Same doctor, same weekday, overlapping hours and overlapping effective periods.</summary>
    public bool OverlapsWith(DoctorSchedule other) =>
        other.Id != Id &&
        other.IsActive && IsActive &&
        other.DoctorId == DoctorId &&
        other.DayOfWeek == DayOfWeek &&
        StartTime < other.EndTime && other.StartTime < EndTime &&
        EffectiveFrom <= (other.EffectiveTo ?? DateOnly.MaxValue) && other.EffectiveFrom <= (EffectiveTo ?? DateOnly.MaxValue);

    public TimeOnly EstimatedTimeFor(int serialNumber)
    {
        var estimate = StartTime.AddMinutes((serialNumber - 1) * AverageMinutesPerPatient);
        // Never estimate past the end of the session (or wrap past midnight).
        return estimate < StartTime || estimate > EndTime ? EndTime : estimate;
    }
}

/// <summary>A date on which a doctor does not see patients (whole day, or a single session).</summary>
[Audited]
public sealed class DoctorLeave : AuditableEntity
{
    private DoctorLeave() { }

    public Guid DoctorId { get; private set; }
    public DateOnly Date { get; private set; }

    /// <summary>Null = all sessions that day.</summary>
    public Guid? ScheduleId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public static DoctorLeave Create(Guid doctorId, DateOnly date, Guid? scheduleId, string reason) =>
        new() { DoctorId = doctorId, Date = date, ScheduleId = scheduleId, Reason = reason.Trim() };

    public bool Covers(Guid scheduleId) => ScheduleId is null || ScheduleId == scheduleId;
}
