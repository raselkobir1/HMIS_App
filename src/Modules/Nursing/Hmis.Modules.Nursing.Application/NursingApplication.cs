using Hmis.BuildingBlocks.Application.Security;
using Hmis.Modules.Nursing.Domain.Entities;

namespace Hmis.Modules.Nursing.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class NursingApplicationMarker;

public static class NursingPermissions
{
    public const string Module = "Nursing";

    public const string View = "Nursing.View";
    public const string Record = "Nursing.Record";
    public const string RosterManage = "Nursing.Roster.Manage";
}

/// <summary>MAR, fluids and nursing notes are clinical records: explicit grant, administrators do not receive them automatically.</summary>
public sealed class NursingPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(NursingPermissions.View, NursingPermissions.Module, "View ward patients, MAR, intake/output, nursing notes and handovers",
            [SystemRoles.Nurse, SystemRoles.Doctor], RequiresExplicitGrant: true),
        new(NursingPermissions.Record, NursingPermissions.Module, "Record medication administration, intake/output, nursing notes and handovers",
            [SystemRoles.Nurse], RequiresExplicitGrant: true),
        new(NursingPermissions.RosterManage, NursingPermissions.Module, "Assign nurses to wards and shifts",
            [SystemRoles.HospitalAdministrator])
    ];
}

public static class NursingErrorCodes
{
    public const string StayNotActive = "IPD_STAY_NOT_ACTIVE";
    public const string OrderInvalid = "MAR_ORDER_INVALID";
    public const string OrderNotActive = "MEDICATION_NOT_ACTIVE";
    public const string NotScheduled = "MAR_NOT_SCHEDULED";
    public const string TooEarly = "MAR_TOO_EARLY";
    public const string AlreadyRecorded = "MAR_ALREADY_RECORDED";
    public const string TimeInvalid = "NURSING_TIME_INVALID";
    public const string WardInvalid = "NURSING_WARD_INVALID";
    public const string EmployeeInvalid = "ROSTER_EMPLOYEE_INVALID";
    public const string RosterDuplicate = "ROSTER_DUPLICATE";
    public const string RosterInCharge = "ROSTER_IN_CHARGE_EXISTS";
    public const string RosterPast = "ROSTER_DATE_PAST";
    public const string HandoverDate = "HANDOVER_DATE_INVALID";
    public const string HandoverPatient = "HANDOVER_PATIENT_INVALID";
}

// ---------- Shared ----------

public sealed record EnteredInErrorDto
{
    public string Reason { get; init; } = string.Empty;
}

public sealed record AllergyDto(string Allergen, string Type, string Severity, string? Reaction);

/// <summary>Header shown on every nursing screen for one patient (with allergies, which matter for every dose).</summary>
public sealed record NursingPatientDto(
    Guid AdmissionId, string AdmissionNumber, string Status, bool IsActive, Guid PatientId, string? PatientName, string? MRNumber,
    string? Gender, DateOnly? DateOfBirth, string? WardName, string? BedNumber, string? AttendingDoctorName, DateTime AdmittedAt,
    IReadOnlyList<AllergyDto> Allergies);

public sealed record WardOptionDto(Guid Id, string Code, string Name);

/// <summary>The shift now and the wards the signed-in nurse is rostered to in it.</summary>
public sealed record CurrentShiftDto(DateOnly Date, Shift Shift, DateTime StartsAt, DateTime EndsAt, IReadOnlyList<WardOptionDto> MyWards,
    IReadOnlyList<WardOptionDto> AllWards);

public sealed record WardPatientDto(
    Guid AdmissionId, string AdmissionNumber, Guid PatientId, string? PatientName, string? MRNumber, string? Gender, DateOnly? DateOfBirth,
    string? BedNumber, string? AttendingDoctorName, string Status, DateTime AdmittedAt, int DueNow, int Overdue, DateTime? NextDoseAt,
    int ActiveOrders);

public sealed record OnDutyDto(Guid EmployeeId, string? Name, bool IsInCharge);

public sealed record WardRoundDto(WardOptionDto Ward, DateOnly Date, Shift Shift, IReadOnlyList<OnDutyDto> OnDuty, IReadOnlyList<WardPatientDto> Patients);

// ---------- MAR ----------

public sealed record AdministrationDto(
    Guid Id, Guid OrderId, DateTime? ScheduledAt, AdministrationStatus Status, DateTime OccurredAt, string? DoseGiven, string? Reason,
    string? Notes, string? RecordedBy, DateTime RecordedAt, bool IsEnteredInError, string? ErrorReason, bool IsMine);

/// <summary>State: Given, Omitted, Refused, Held (recorded) or Due, Overdue, Upcoming, Cancelled (not recorded).</summary>
public sealed record DoseSlotDto(DateTime ScheduledAt, string State, AdministrationDto? Entry);

public sealed record MarOrderDto(
    Guid Id, string MedicineName, string? Strength, string? Form, string Route, string Dose, string Frequency, DateTime StartsAt,
    DateTime? EndsAt, string Status, string? PrnIndication, string? Instructions, string? OrderedByName, IReadOnlyList<DoseSlotDto> Slots,
    // PRN doses and every entry of the day (including ones marked in error, kept for the record).
    IReadOnlyList<AdministrationDto> Entries);

public sealed record MarDto(NursingPatientDto Patient, DateOnly Date, IReadOnlyList<MarOrderDto> Orders);

public sealed record AdministrationCreateDto
{
    public Guid OrderId { get; init; }

    /// <summary>The scheduled dose time; omit for a PRN dose.</summary>
    public DateTime? ScheduledAt { get; init; }

    public AdministrationStatus Status { get; init; }

    /// <summary>Defaults to now.</summary>
    public DateTime? OccurredAt { get; init; }

    public string? DoseGiven { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
}

// ---------- Intake / output ----------

public sealed record IntakeOutputEntryDto(
    Guid Id, DateTime MeasuredAt, FluidDirection Direction, string Category, int VolumeMl, string? Notes, string? RecordedBy,
    bool IsEnteredInError, string? ErrorReason, bool IsMine);

public sealed record FluidTotalsDto(string Label, int Intake, int Output, int Balance);

/// <summary>A fluid day runs from 8am to 8am (hospital time), matching the start of the morning shift.</summary>
public sealed record IntakeOutputDayDto(
    NursingPatientDto Patient, DateOnly Date, DateTime From, DateTime To, FluidTotalsDto Total, IReadOnlyList<FluidTotalsDto> ByShift,
    IReadOnlyList<IntakeOutputEntryDto> Entries, IReadOnlyList<string> IntakeCategories, IReadOnlyList<string> OutputCategories);

public sealed record IntakeOutputCreateDto
{
    /// <summary>Defaults to now.</summary>
    public DateTime? MeasuredAt { get; init; }

    public FluidDirection Direction { get; init; }
    public string Category { get; init; } = string.Empty;
    public int VolumeMl { get; init; }
    public string? Notes { get; init; }
}

// ---------- Nursing notes ----------

public sealed record NursingNoteDto(
    Guid Id, NursingNoteKind Kind, string Text, string? WrittenBy, DateTime WrittenAt, bool IsEnteredInError, string? ErrorReason, bool IsMine);

public sealed record NursingNoteCreateDto
{
    public NursingNoteKind Kind { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed record NursingNotesDto(NursingPatientDto Patient, IReadOnlyList<NursingNoteDto> Notes);

// ---------- Handover ----------

public sealed record HandoverItemDto(Guid AdmissionId, string? PatientName, string? MRNumber, string? BedNumber, string Note);

public sealed record HandoverDto(
    Guid Id, Guid WardId, string? WardName, DateOnly Date, Shift Shift, string? Summary, IReadOnlyList<HandoverItemDto> Items, string? WrittenBy,
    DateTime WrittenAt, string? ReceivedBy, DateTime? ReceivedAt, bool IsMine, uint RowVersion);

public sealed record HandoverItemSaveDto
{
    public Guid AdmissionId { get; init; }
    public string Note { get; init; } = string.Empty;
}

public sealed record HandoverSaveDto
{
    public Guid WardId { get; init; }
    public DateOnly Date { get; init; }
    public Shift Shift { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<HandoverItemSaveDto> Items { get; init; } = [];

    /// <summary>Required when revising an existing handover.</summary>
    public uint RowVersion { get; init; }
}

public sealed record HandoverReceiveDto
{
    public uint RowVersion { get; init; }
}

public sealed class HandoverQuery
{
    public Guid WardId { get; set; }
    public DateOnly? Date { get; set; }
}

// ---------- Roster ----------

public sealed record RosterEntryDto(
    Guid Id, Guid WardId, string? WardName, DateOnly Date, Shift Shift, Guid EmployeeId, string? EmployeeName, string? EmployeeCode, bool IsInCharge);

public sealed record RosterCreateDto
{
    public Guid WardId { get; init; }
    public DateOnly Date { get; init; }
    public Shift Shift { get; init; }
    public Guid EmployeeId { get; init; }
    public bool IsInCharge { get; init; }
}

public sealed class RosterQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? WardId { get; set; }
}
