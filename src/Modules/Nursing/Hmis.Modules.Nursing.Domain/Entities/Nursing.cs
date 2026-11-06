using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Nursing.Domain.Entities;

/// <summary>Fixed ward shifts (hospital time): Morning 8am–2pm, Evening 2pm–8pm, Night 8pm–8am (belongs to the date it starts).</summary>
public enum Shift
{
    Morning,
    Evening,
    Night
}

/// <summary>A nurse (HR employee) on duty in a ward for one shift. A soft roster: it sets "my ward", it does not limit access.</summary>
[Audited]
public sealed class NurseAssignment : AuditableEntity, IAggregateRoot
{
    private NurseAssignment() { }

    public Guid WardId { get; private set; }
    public DateOnly Date { get; private set; }
    public Shift Shift { get; private set; }
    public Guid EmployeeId { get; private set; }
    public bool IsInCharge { get; private set; }

    public static NurseAssignment Create(Guid wardId, DateOnly date, Shift shift, Guid employeeId, bool isInCharge) =>
        new() { WardId = wardId, Date = date, Shift = shift, EmployeeId = employeeId, IsInCharge = isInCharge };

    public void SetInCharge(bool isInCharge) => IsInCharge = isInCharge;
}

public enum AdministrationStatus
{
    Given,
    Omitted,
    Refused,
    Held
}

public sealed record AdministrationValues(AdministrationStatus Status, DateTime OccurredAt, string? DoseGiven, string? Reason, string? Notes);

/// <summary>
/// One MAR entry, always linked to a medication order (BRD Module 07). A scheduled dose has ScheduledAt; PRN doses do not.
/// Never edited or deleted: the nurse who recorded it can mark it as entered in error, which frees the dose to be recorded again.
/// </summary>
[Audited]
public sealed class MedicationAdministration : AuditableEntity, IAggregateRoot
{
    private MedicationAdministration() { }

    public Guid MedicationOrderId { get; private set; }
    public Guid AdmissionId { get; private set; }
    public Guid PatientId { get; private set; }
    public DateTime? ScheduledAt { get; private set; }
    public AdministrationStatus Status { get; private set; }

    /// <summary>When the dose was given, or when the decision not to give it was made.</summary>
    public DateTime OccurredAt { get; private set; }

    public string? DoseGiven { get; private set; }
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public bool IsEnteredInError { get; private set; }
    public string? ErrorReason { get; private set; }

    public static MedicationAdministration Record(Guid orderId, Guid admissionId, Guid patientId, DateTime? scheduledAt, AdministrationValues v)
    {
        if (v.Status != AdministrationStatus.Given && string.IsNullOrWhiteSpace(v.Reason))
        {
            throw new DomainException("MAR_REASON_REQUIRED", "Give the reason the dose was not given.");
        }

        return new MedicationAdministration
        {
            MedicationOrderId = orderId,
            AdmissionId = admissionId,
            PatientId = patientId,
            ScheduledAt = scheduledAt,
            Status = v.Status,
            OccurredAt = v.OccurredAt,
            DoseGiven = Clean(v.DoseGiven),
            Reason = v.Status == AdministrationStatus.Given ? null : v.Reason!.Trim(),
            Notes = Clean(v.Notes)
        };
    }

    public void MarkEnteredInError(Guid userId, string reason)
    {
        if (CreatedBy != userId)
        {
            throw new DomainException("MAR_NOT_YOURS", "Only the nurse who recorded this dose can mark it as entered in error.");
        }

        if (IsEnteredInError)
        {
            throw new DomainException("MAR_ALREADY_IN_ERROR", "This entry is already marked as entered in error.");
        }

        IsEnteredInError = true;
        ErrorReason = reason.Trim();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum FluidDirection
{
    Intake,
    Output
}

/// <summary>Intake: Oral, IntravenousFluid, Tube, Blood, Other. Output: Urine, Vomit, Drain, Stool, Aspirate, Other.</summary>
public static class FluidCategories
{
    public static readonly IReadOnlyList<string> Intake = ["Oral", "IV fluid", "Tube feed", "Blood", "Other"];
    public static readonly IReadOnlyList<string> Output = ["Urine", "Vomit", "Drain", "Stool", "Aspirate", "Other"];

    public static bool IsValid(FluidDirection direction, string category) =>
        (direction == FluidDirection.Intake ? Intake : Output).Contains(category);
}

[Audited]
public sealed class IntakeOutputEntry : AuditableEntity, IAggregateRoot
{
    private IntakeOutputEntry() { }

    public Guid AdmissionId { get; private set; }
    public Guid PatientId { get; private set; }
    public DateTime MeasuredAt { get; private set; }
    public FluidDirection Direction { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public int VolumeMl { get; private set; }
    public string? Notes { get; private set; }
    public bool IsEnteredInError { get; private set; }
    public string? ErrorReason { get; private set; }

    public static IntakeOutputEntry Record(Guid admissionId, Guid patientId, DateTime measuredAt, FluidDirection direction, string category,
        int volumeMl, string? notes)
    {
        if (!FluidCategories.IsValid(direction, category))
        {
            throw new DomainException("FLUID_CATEGORY_INVALID", $"Choose a valid {direction.ToString().ToLowerInvariant()} type.");
        }

        return new IntakeOutputEntry
        {
            AdmissionId = admissionId,
            PatientId = patientId,
            MeasuredAt = measuredAt,
            Direction = direction,
            Category = category,
            VolumeMl = volumeMl,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public void MarkEnteredInError(string reason)
    {
        if (IsEnteredInError)
        {
            throw new DomainException("FLUID_ALREADY_IN_ERROR", "This entry is already marked as entered in error.");
        }

        IsEnteredInError = true;
        ErrorReason = reason.Trim();
    }
}

public enum NursingNoteKind
{
    Assessment,
    CarePlan,
    Progress,
    Incident
}

/// <summary>Nursing assessment, care plan, progress or incident note. Signed when saved; corrections by "entered in error".</summary>
[Audited]
public sealed class NursingNote : AuditableEntity, IAggregateRoot
{
    private NursingNote() { }

    public Guid AdmissionId { get; private set; }
    public Guid PatientId { get; private set; }
    public NursingNoteKind Kind { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsEnteredInError { get; private set; }
    public string? ErrorReason { get; private set; }

    public static NursingNote Write(Guid admissionId, Guid patientId, NursingNoteKind kind, string text) =>
        new() { AdmissionId = admissionId, PatientId = patientId, Kind = kind, Text = text.Trim() };

    public void MarkEnteredInError(Guid userId, string reason)
    {
        if (CreatedBy != userId)
        {
            throw new DomainException("NURSING_NOTE_NOT_YOURS", "Only the nurse who wrote this note can mark it as entered in error.");
        }

        if (IsEnteredInError)
        {
            throw new DomainException("NURSING_NOTE_ALREADY_IN_ERROR", "This note is already marked as entered in error.");
        }

        IsEnteredInError = true;
        ErrorReason = reason.Trim();
    }
}

public sealed record HandoverItemValues(Guid AdmissionId, string Note);

/// <summary>
/// The outgoing shift's handover for a ward. The author can revise it until the incoming nurse acknowledges it; after that it
/// is read-only.
/// </summary>
[Audited]
public sealed class ShiftHandover : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private readonly List<HandoverItem> _items = [];

    private ShiftHandover() { }

    public Guid WardId { get; private set; }
    public DateOnly Date { get; private set; }
    public Shift Shift { get; private set; }
    public string? Summary { get; private set; }
    public Guid? ReceivedBy { get; private set; }
    public DateTime? ReceivedAt { get; private set; }

    /// <summary>Incremented on every change so the RowVersion check also covers item changes.</summary>
    public int Revision { get; private set; }

    public uint RowVersion { get; private set; }

    public IReadOnlyList<HandoverItem> Items => _items;

    public static ShiftHandover Create(Guid wardId, DateOnly date, Shift shift, string? summary, IReadOnlyList<HandoverItemValues> items)
    {
        var handover = new ShiftHandover { WardId = wardId, Date = date, Shift = shift };
        handover.Revise(summary, items);
        return handover;
    }

    public void Update(Guid userId, string? summary, IReadOnlyList<HandoverItemValues> items)
    {
        if (ReceivedAt is not null)
        {
            throw new DomainException("HANDOVER_RECEIVED", "This handover has been received and can no longer be changed.");
        }

        if (CreatedBy != userId)
        {
            throw new DomainException("HANDOVER_NOT_YOURS", "Only the nurse who wrote this handover can change it.");
        }

        Revise(summary, items);
    }

    public void Receive(Guid userId, DateTime at)
    {
        if (ReceivedAt is not null)
        {
            throw new DomainException("HANDOVER_RECEIVED", "This handover has already been received.");
        }

        if (CreatedBy == userId)
        {
            throw new DomainException("HANDOVER_SELF_RECEIVE", "The incoming nurse receives the handover, not its author.");
        }

        ReceivedBy = userId;
        ReceivedAt = at;
        Revision++;
    }

    private void Revise(string? summary, IReadOnlyList<HandoverItemValues> items)
    {
        var cleanSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        var cleanItems = items.Where(i => !string.IsNullOrWhiteSpace(i.Note)).ToList();
        if (cleanSummary is null && cleanItems.Count == 0)
        {
            throw new DomainException("HANDOVER_EMPTY", "Write a ward summary or a note for at least one patient.");
        }

        if (cleanItems.GroupBy(i => i.AdmissionId).Any(g => g.Count() > 1))
        {
            throw new DomainException("HANDOVER_DUPLICATE_PATIENT", "Write one handover note per patient.");
        }

        Summary = cleanSummary;
        _items.Clear();
        _items.AddRange(cleanItems.Select((i, index) => HandoverItem.Create(Id, i.AdmissionId, i.Note.Trim(), index)));
        Revision++;
    }
}

/// <summary>One patient's note in a handover. Audited like the handover itself: every revision's items are captured.</summary>
[Audited]
public sealed class HandoverItem : BaseEntity
{
    private HandoverItem() { }

    public Guid HandoverId { get; private set; }
    public Guid AdmissionId { get; private set; }
    public string Note { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    internal static HandoverItem Create(Guid handoverId, Guid admissionId, string note, int sortOrder) =>
        new() { HandoverId = handoverId, AdmissionId = admissionId, Note = note, SortOrder = sortOrder };
}
