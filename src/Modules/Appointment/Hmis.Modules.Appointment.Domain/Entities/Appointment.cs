using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Appointment.Domain.Entities;

public enum VisitType
{
    New,
    FollowUp
}

public enum AppointmentSource
{
    Reception,
    Phone,
    WalkIn
}

/// <summary>
/// BRD statuses. "Checked In" and "Waiting" are one state: checking in places the patient in the queue.
/// </summary>
public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Waiting,
    InConsultation,
    Completed,
    Cancelled,
    NoShow
}

/// <summary>
/// A booked visit with a doctor in a specific session on a specific date. The serial number is the patient's
/// queue token for that session; the fee is a snapshot of the doctor's fee at booking (for Billing, Phase 3).
/// </summary>
[Audited]
public sealed class Appointment : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private Appointment() { }

    public string AppointmentNumber { get; private set; } = string.Empty;
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid ScheduleId { get; private set; }
    public DateOnly AppointmentDate { get; private set; }
    public int SerialNumber { get; private set; }
    public TimeOnly EstimatedTime { get; private set; }
    public VisitType VisitType { get; private set; }
    public AppointmentSource Source { get; private set; }
    public decimal Fee { get; private set; }
    public string? Notes { get; private set; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Scheduled;
    public DateTime? CheckedInAt { get; private set; }
    public DateTime? ConsultationStartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public uint RowVersion { get; private set; }

    /// <summary>Statuses that hold a place in a session (count toward capacity and the one-per-doctor-per-day rule).</summary>
    public static readonly AppointmentStatus[] ActiveStatuses =
        [AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.Waiting, AppointmentStatus.InConsultation, AppointmentStatus.Completed];

    public static Appointment Book(
        string appointmentNumber, Guid patientId, Guid doctorId, Guid scheduleId, DateOnly date, int serialNumber,
        TimeOnly estimatedTime, VisitType visitType, AppointmentSource source, decimal fee, string? notes) =>
        new()
        {
            AppointmentNumber = appointmentNumber,
            PatientId = patientId,
            DoctorId = doctorId,
            ScheduleId = scheduleId,
            AppointmentDate = date,
            SerialNumber = serialNumber,
            EstimatedTime = estimatedTime,
            VisitType = visitType,
            Source = source,
            Fee = fee,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

    public void Confirm()
    {
        Require(AppointmentStatus.Scheduled);
        Status = AppointmentStatus.Confirmed;
    }

    /// <summary>Patient has arrived; joins the queue. Only on the appointment date.</summary>
    public void CheckIn(DateOnly today, DateTime utcNow)
    {
        Require(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);
        if (AppointmentDate != today)
        {
            throw new DomainException("APPOINTMENT_NOT_TODAY", "Patients can only be checked in on the appointment date.");
        }

        Status = AppointmentStatus.Waiting;
        CheckedInAt = utcNow;
    }

    public void StartConsultation(DateTime utcNow)
    {
        Require(AppointmentStatus.Waiting);
        Status = AppointmentStatus.InConsultation;
        ConsultationStartedAt = utcNow;
    }

    public void Complete(DateTime utcNow)
    {
        Require(AppointmentStatus.InConsultation);
        Status = AppointmentStatus.Completed;
        CompletedAt = utcNow;
    }

    /// <summary>Cancelling is possible until the consultation starts (a waiting patient may leave).</summary>
    public void Cancel(string reason, DateTime utcNow)
    {
        Require(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.Waiting);
        Status = AppointmentStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = reason.Trim();
    }

    /// <summary>The patient did not arrive. Only on or after the appointment date.</summary>
    public void MarkNoShow(DateOnly today)
    {
        Require(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);
        if (AppointmentDate > today)
        {
            throw new DomainException("APPOINTMENT_IN_FUTURE", "A future appointment cannot be marked as no-show.");
        }

        Status = AppointmentStatus.NoShow;
    }

    /// <summary>Moves to another session/date with a new serial; the appointment number stays the same.</summary>
    public void Reschedule(Guid scheduleId, DateOnly date, int serialNumber, TimeOnly estimatedTime)
    {
        Require(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);
        ScheduleId = scheduleId;
        AppointmentDate = date;
        SerialNumber = serialNumber;
        EstimatedTime = estimatedTime;
        Status = AppointmentStatus.Scheduled;
    }

    private void Require(params AppointmentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException("APPOINTMENT_INVALID_STATUS",
                $"This action is not possible while the appointment is {Status}.");
        }
    }
}
