using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Clinical.Domain.Entities;

public enum ProgressNoteKind
{
    WardRound,
    OnCall,
    ConsultantReview,
    Procedure
}

public sealed record ProgressNoteValues(ProgressNoteKind Kind, string? Subjective, string? Objective, string? Assessment, string? Plan);

/// <summary>
/// A doctor's note during an inpatient stay (SOAP). Signed when saved: never edited or deleted; the author can mark it as
/// entered in error and write a new one.
/// </summary>
[Audited]
public sealed class ProgressNote : AuditableEntity, IAggregateRoot
{
    private ProgressNote() { }

    public Guid AdmissionId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public ProgressNoteKind Kind { get; private set; }
    public string? Subjective { get; private set; }
    public string? Objective { get; private set; }
    public string? Assessment { get; private set; }
    public string? Plan { get; private set; }
    public bool IsEnteredInError { get; private set; }
    public string? ErrorReason { get; private set; }

    public static ProgressNote Write(Guid admissionId, Guid patientId, Guid doctorId, ProgressNoteValues v)
    {
        var note = new ProgressNote
        {
            AdmissionId = admissionId,
            PatientId = patientId,
            DoctorId = doctorId,
            Kind = v.Kind,
            Subjective = Clean(v.Subjective),
            Objective = Clean(v.Objective),
            Assessment = Clean(v.Assessment),
            Plan = Clean(v.Plan)
        };
        if (note is { Subjective: null, Objective: null, Assessment: null, Plan: null })
        {
            throw new DomainException("PROGRESS_NOTE_EMPTY", "Write at least one part of the note.");
        }

        return note;
    }

    public void MarkEnteredInError(Guid doctorId, string reason)
    {
        if (doctorId != DoctorId)
        {
            throw new DomainException("PROGRESS_NOTE_NOT_YOURS", "Only the doctor who wrote this note can mark it as entered in error.");
        }

        if (IsEnteredInError)
        {
            throw new DomainException("PROGRESS_NOTE_ALREADY_IN_ERROR", "This note is already marked as entered in error.");
        }

        IsEnteredInError = true;
        ErrorReason = reason.Trim();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum MedicationOrderStatus
{
    Active,
    Stopped
}

public sealed record MedicationOrderValues(
    string MedicineName,
    string? Strength,
    string? Form,
    string Route,
    string Dose,
    string Frequency,
    DateTime StartsAt,
    int? DurationDays,
    string? PrnIndication,
    string? Instructions);

/// <summary>
/// An inpatient medication order (free-text medicine until the Pharmacy item master exists). Orders are never edited:
/// a change is a stop plus a new order, so every MAR entry stays linked to exactly what was ordered.
/// </summary>
[Audited]
public sealed class MedicationOrder : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    public const string StatFrequency = "STAT";
    public const string PrnFrequency = "PRN";

    private MedicationOrder() { }

    public Guid AdmissionId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid OrderedByDoctorId { get; private set; }
    public string MedicineName { get; private set; } = string.Empty;
    public string? Strength { get; private set; }
    public string? Form { get; private set; }
    public string Route { get; private set; } = string.Empty;
    public string Dose { get; private set; } = string.Empty;
    public string Frequency { get; private set; } = string.Empty;
    public DateTime StartsAt { get; private set; }

    /// <summary>Planned length; null means until stopped. Not used for STAT.</summary>
    public int? DurationDays { get; private set; }

    public string? PrnIndication { get; private set; }
    public string? Instructions { get; private set; }
    public MedicationOrderStatus Status { get; private set; }
    public DateTime? StoppedAt { get; private set; }
    public Guid? StoppedByDoctorId { get; private set; }
    public string? StopReason { get; private set; }
    public uint RowVersion { get; private set; }

    /// <summary>Planned end: start + duration; a STAT order stays on the active list for a day.</summary>
    public DateTime? PlannedEndsAt => Frequency == StatFrequency ? StartsAt.AddDays(1) : DurationDays is { } days ? StartsAt.AddDays(days) : null;

    /// <summary>When doses stop: the stop time if stopped before the planned end, otherwise the planned end.</summary>
    public DateTime? EndsAt => StoppedAt is { } stopped && (PlannedEndsAt is null || stopped < PlannedEndsAt) ? stopped : PlannedEndsAt;

    public static MedicationOrder Create(Guid admissionId, Guid patientId, Guid doctorId, MedicationOrderValues v)
    {
        var isPrn = v.Frequency == PrnFrequency;
        if (isPrn && string.IsNullOrWhiteSpace(v.PrnIndication))
        {
            throw new DomainException("MEDICATION_PRN_INDICATION", "Say when the PRN medicine should be given (e.g. fever above 100°F).");
        }

        if (v.Frequency != StatFrequency && v.DurationDays is < 1)
        {
            throw new DomainException("MEDICATION_DURATION", "The duration must be at least one day.");
        }

        return new MedicationOrder
        {
            AdmissionId = admissionId,
            PatientId = patientId,
            OrderedByDoctorId = doctorId,
            MedicineName = v.MedicineName.Trim(),
            Strength = Clean(v.Strength),
            Form = Clean(v.Form),
            Route = v.Route.Trim(),
            Dose = v.Dose.Trim(),
            Frequency = v.Frequency,
            StartsAt = v.StartsAt,
            DurationDays = v.Frequency == StatFrequency ? null : v.DurationDays,
            PrnIndication = isPrn ? v.PrnIndication!.Trim() : null,
            Instructions = Clean(v.Instructions),
            Status = MedicationOrderStatus.Active
        };
    }

    /// <summary>"Completed" once the planned end has passed; stopped orders stay "Stopped".</summary>
    public string StatusAt(DateTime now) =>
        Status == MedicationOrderStatus.Stopped ? "Stopped" : PlannedEndsAt is { } end && end <= now ? "Completed" : "Active";

    public void Stop(Guid doctorId, string reason, DateTime at)
    {
        if (StatusAt(at) != "Active")
        {
            throw new DomainException("MEDICATION_NOT_ACTIVE", "This order is no longer active.");
        }

        Status = MedicationOrderStatus.Stopped;
        StoppedAt = at;
        StoppedByDoctorId = doctorId;
        StopReason = reason.Trim();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
