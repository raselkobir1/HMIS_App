using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.Modules.Appointment.Domain.Entities;

namespace Hmis.Modules.Appointment.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class AppointmentApplicationMarker;

public static class AppointmentPermissions
{
    public const string Module = "Appointment";

    public const string View = "Appointment.View";
    public const string Book = "Appointment.Book";
    public const string CheckIn = "Appointment.CheckIn";
    public const string Cancel = "Appointment.Cancel";
    public const string Consult = "Appointment.Consult";
    public const string SchedulesManage = "Appointment.Schedules.Manage";
    public const string QueueDisplay = "Appointment.QueueDisplay";
}

public sealed class AppointmentPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(AppointmentPermissions.View, AppointmentPermissions.Module, "View appointments, sessions and availability",
            [SystemRoles.HospitalAdministrator, SystemRoles.Receptionist, SystemRoles.Doctor, SystemRoles.Nurse]),
        new(AppointmentPermissions.Book, AppointmentPermissions.Module, "Book, confirm and reschedule appointments", [SystemRoles.Receptionist]),
        new(AppointmentPermissions.CheckIn, AppointmentPermissions.Module, "Check patients in and mark no-shows", [SystemRoles.Receptionist]),
        new(AppointmentPermissions.Cancel, AppointmentPermissions.Module, "Cancel appointments", [SystemRoles.Receptionist]),
        new(AppointmentPermissions.Consult, AppointmentPermissions.Module, "Run own consultation queue (call next, complete)", [SystemRoles.Doctor]),
        new(AppointmentPermissions.SchedulesManage, AppointmentPermissions.Module, "Manage doctor sessions and leave", [SystemRoles.HospitalAdministrator]),
        new(AppointmentPermissions.QueueDisplay, AppointmentPermissions.Module, "Open the queue display board (no patient names)",
            [SystemRoles.HospitalAdministrator, SystemRoles.Receptionist, SystemRoles.Nurse])
    ];
}

public static class AppointmentErrorCodes
{
    public const string DoctorUnavailable = "APPOINTMENT_DOCTOR_UNAVAILABLE";
    public const string PatientUnavailable = "APPOINTMENT_PATIENT_UNAVAILABLE";
    public const string SessionInvalid = "APPOINTMENT_SESSION_INVALID";
    public const string SessionFull = "APPOINTMENT_SESSION_FULL";
    public const string SessionEnded = "APPOINTMENT_SESSION_ENDED";
    public const string DoctorOnLeave = "APPOINTMENT_DOCTOR_ON_LEAVE";
    public const string DateOutOfRange = "APPOINTMENT_DATE_OUT_OF_RANGE";
    public const string AlreadyBooked = "APPOINTMENT_ALREADY_BOOKED";
    public const string NotYourPatient = "APPOINTMENT_NOT_YOUR_PATIENT";
    public const string ConsultationInProgress = "APPOINTMENT_CONSULTATION_IN_PROGRESS";
    public const string QueueEmpty = "APPOINTMENT_QUEUE_EMPTY";
    public const string ScheduleOverlap = "SCHEDULE_OVERLAP";
    public const string ScheduleInUse = "SCHEDULE_IN_USE";
    public const string LeaveConflict = "LEAVE_HAS_APPOINTMENTS";
    public const string LeaveExists = "LEAVE_ALREADY_RECORDED";
}

/// <summary>APPOINTMENT numbers are configured by Administration (seeded as APT + YY + 6 digits, yearly reset).</summary>
public static class AppointmentSequences
{
    public const string AppointmentNumber = "APPOINTMENT";
}

/// <summary>Bookings are accepted up to this many days ahead.</summary>
public static class BookingWindow
{
    public const int MaxDaysAhead = 90;
}

// ---------- Schedules & leave ----------

public sealed record ScheduleDto(
    Guid Id, Guid DoctorId, string? DoctorName, string? Specialization, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
    int MaxPatients, int AverageMinutesPerPatient, string? Room, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive,
    uint RowVersion);

public sealed record ScheduleCreateDto
{
    public Guid DoctorId { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int MaxPatients { get; init; }
    public int AverageMinutesPerPatient { get; init; }
    public string? Room { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
}

public sealed record ScheduleUpdateDto
{
    public DayOfWeek DayOfWeek { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int MaxPatients { get; init; }
    public int AverageMinutesPerPatient { get; init; }
    public string? Room { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record ScheduleStatusDto
{
    public bool IsActive { get; init; }
}

public sealed class ScheduleQuery
{
    public Guid? DoctorId { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record LeaveDto(Guid Id, Guid DoctorId, string? DoctorName, DateOnly Date, Guid? ScheduleId, string? SessionLabel, string Reason);

public sealed record LeaveCreateDto
{
    public Guid DoctorId { get; init; }
    public DateOnly Date { get; init; }
    public Guid? ScheduleId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class LeaveQuery
{
    public Guid? DoctorId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

// ---------- Availability & booking ----------

public sealed class AvailabilityQuery
{
    public Guid DoctorId { get; set; }
    public DateOnly Date { get; set; }
}

public sealed record SessionAvailabilityDto(
    Guid ScheduleId, TimeOnly StartTime, TimeOnly EndTime, string? Room, int MaxPatients, int Booked, int Remaining,
    int NextSerial, TimeOnly NextEstimatedTime, bool IsFull, bool HasEnded, bool OnLeave, string? LeaveReason, bool IsBookable);

public sealed record AppointmentBookDto
{
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid ScheduleId { get; init; }
    public DateOnly AppointmentDate { get; init; }
    public VisitType VisitType { get; init; }
    public AppointmentSource Source { get; init; }
    public string? Notes { get; init; }
}

public sealed record AppointmentRescheduleDto
{
    public Guid ScheduleId { get; init; }
    public DateOnly AppointmentDate { get; init; }
    public uint RowVersion { get; init; }
}

public sealed record AppointmentCancelDto
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class AppointmentListQuery : PaginationRequest
{
    public DateOnly? Date { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? PatientId { get; set; }
    public AppointmentStatus? Status { get; set; }

    /// <summary>Set by the manager: the patient plus duplicate records merged into it (overrides PatientId).</summary>
    public IReadOnlyList<Guid>? PatientIds { get; set; }
}

public sealed record StatusCountDto(AppointmentStatus Status, int Count);

/// <summary>Appointments of one day by status (dashboard).</summary>
public sealed record AppointmentDaySummaryDto(DateOnly Date, int Total, IReadOnlyList<StatusCountDto> ByStatus);

public sealed record AppointmentDto(
    Guid Id,
    string AppointmentNumber,
    Guid PatientId,
    string? PatientMRNumber,
    string? PatientName,
    string? PatientGender,
    DateOnly? PatientDateOfBirth,
    Guid DoctorId,
    string? DoctorName,
    Guid ScheduleId,
    string? Room,
    DateOnly AppointmentDate,
    int SerialNumber,
    TimeOnly EstimatedTime,
    VisitType VisitType,
    AppointmentSource Source,
    decimal Fee,
    string? Notes,
    AppointmentStatus Status,
    DateTime? CheckedInAt,
    DateTime? ConsultationStartedAt,
    DateTime? CompletedAt,
    string? CancellationReason,
    uint RowVersion);

// ---------- Queues ----------

/// <summary>Public queue board: serial numbers only, never patient names (BRD Module 03).</summary>
public sealed record QueueSessionDto(
    Guid ScheduleId, string? DoctorName, string? Specialization, string? Room, TimeOnly StartTime, TimeOnly EndTime,
    int? NowServing, IReadOnlyList<int> Waiting, int CompletedCount);

public sealed record QueueBoardDto(DateOnly Date, DateTime GeneratedAt, IReadOnlyList<QueueSessionDto> Sessions);

/// <summary>A doctor's own queue for a day (full patient details for the treating doctor).</summary>
public sealed record MyQueueDto(Guid DoctorId, string? DoctorName, DateOnly Date, AppointmentDto? Current, IReadOnlyList<AppointmentDto> Waiting,
    IReadOnlyList<AppointmentDto> Others);

public sealed class QueueQuery
{
    public DateOnly? Date { get; set; }
}

/// <summary>Resolved names for lists (from Patient and HR contracts).</summary>
internal sealed record NameMaps(
    IReadOnlyDictionary<Guid, BuildingBlocks.Contracts.Patient.PatientSummary> Patients,
    IReadOnlyDictionary<Guid, BuildingBlocks.Contracts.HR.DoctorSummary> Doctors);
