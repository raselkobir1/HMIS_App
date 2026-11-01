using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Clinical.Domain.Entities;

/// <summary>Where the record belongs. For IPD the encounter id is the admission id (vitals); Emergency follows later.</summary>
public enum EncounterType
{
    Opd,
    Ipd
}

public enum ConsultationStatus
{
    Draft,
    Finalized
}

public enum DiagnosisType
{
    Primary,
    Secondary
}

public enum MedicineTiming
{
    AfterFood,
    BeforeFood,
    WithFood,
    AnyTime
}

public enum DurationUnit
{
    Days,
    Weeks,
    Months,
    Continue
}

/// <summary>Values for one diagnosis line; Id is null for a new line.</summary>
public sealed record DiagnosisValues(Guid? Id, DiagnosisType Type, string Description);

/// <summary>Values for one prescription line; Id is null for a new line.</summary>
public sealed record PrescriptionValues(
    Guid? Id,
    string MedicineName,
    string? Form,
    string? Strength,
    string? Route,
    string DosePattern,
    MedicineTiming Timing,
    int? DurationValue,
    DurationUnit DurationUnit,
    int? Quantity,
    string? Instructions);

/// <summary>Values for one investigation request; Id is null for a new line.</summary>
public sealed record InvestigationValues(Guid? Id, string Name, string? Notes);

/// <summary>
/// The doctor's record of one encounter: history, examination, diagnosis, prescription, investigations and follow-up.
/// Editable only while Draft. Once finalized it is never overwritten; corrections are added as addenda (BRD Module 04).
/// </summary>
[Audited]
public sealed class Consultation : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private readonly List<ConsultationDiagnosis> _diagnoses = [];
    private readonly List<PrescriptionItem> _prescription = [];
    private readonly List<InvestigationRequest> _investigations = [];
    private readonly List<ConsultationAddendum> _addenda = [];

    private Consultation() { }

    public EncounterType EncounterType { get; private set; }
    public Guid EncounterId { get; private set; }
    public string EncounterNumber { get; private set; } = string.Empty;
    public DateOnly EncounterDate { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public ConsultationStatus Status { get; private set; } = ConsultationStatus.Draft;

    public string? ChiefComplaint { get; private set; }
    public string? HistoryOfPresentIllness { get; private set; }
    public string? Examination { get; private set; }
    public string? Advice { get; private set; }
    public DateOnly? FollowUpDate { get; private set; }
    public string? FollowUpInstructions { get; private set; }
    public string? ReferralTo { get; private set; }
    public string? ReferralReason { get; private set; }

    public DateTime? FinalizedAt { get; private set; }

    /// <summary>
    /// Incremented on every draft save so the row itself always changes. That makes the RowVersion check catch a
    /// conflicting edit even when only prescription or diagnosis lines changed.
    /// </summary>
    public int Revision { get; private set; }

    public uint RowVersion { get; private set; }

    public IReadOnlyList<ConsultationDiagnosis> Diagnoses => _diagnoses;
    public IReadOnlyList<PrescriptionItem> Prescription => _prescription;
    public IReadOnlyList<InvestigationRequest> Investigations => _investigations;
    public IReadOnlyList<ConsultationAddendum> Addenda => _addenda;

    public static Consultation Open(EncounterType encounterType, Guid encounterId, string encounterNumber, DateOnly encounterDate,
        Guid patientId, Guid doctorId) =>
        new()
        {
            EncounterType = encounterType,
            EncounterId = encounterId,
            EncounterNumber = encounterNumber,
            EncounterDate = encounterDate,
            PatientId = patientId,
            DoctorId = doctorId
        };

    public void UpdateNotes(string? chiefComplaint, string? historyOfPresentIllness, string? examination, string? advice,
        DateOnly? followUpDate, string? followUpInstructions, string? referralTo, string? referralReason)
    {
        EnsureDraft();
        if (followUpDate is { } date && date <= EncounterDate)
        {
            throw new DomainException("CONSULTATION_FOLLOW_UP_DATE", "The follow-up date must be after the visit date.");
        }

        ChiefComplaint = Clean(chiefComplaint);
        HistoryOfPresentIllness = Clean(historyOfPresentIllness);
        Examination = Clean(examination);
        Advice = Clean(advice);
        FollowUpDate = followUpDate;
        FollowUpInstructions = Clean(followUpInstructions);
        ReferralTo = Clean(referralTo);
        ReferralReason = Clean(referralReason);
        Revision++;
    }

    public void SetDiagnoses(IReadOnlyList<DiagnosisValues> values)
    {
        EnsureDraft();
        if (values.Count(v => v.Type == DiagnosisType.Primary) > 1)
        {
            throw new DomainException("CONSULTATION_DIAGNOSIS_PRIMARY", "Only one primary diagnosis is allowed.");
        }

        Sync(_diagnoses, values, v => v.Id,
            (v, order) => ConsultationDiagnosis.Create(Id, v.Type, v.Description, order),
            (item, v, order) => item.Update(v.Type, v.Description, order));
    }

    public void SetPrescription(IReadOnlyList<PrescriptionValues> values)
    {
        EnsureDraft();
        Sync(_prescription, values, v => v.Id,
            (v, order) => PrescriptionItem.Create(Id, v, order),
            (item, v, order) => item.Update(v, order));
    }

    public void SetInvestigations(IReadOnlyList<InvestigationValues> values)
    {
        EnsureDraft();
        Sync(_investigations, values, v => v.Id,
            (v, order) => InvestigationRequest.Create(Id, v.Name, v.Notes, order),
            (item, v, order) => item.Update(v.Name, v.Notes, order));
    }

    /// <summary>Signs off the record. Requires a chief complaint and exactly one primary diagnosis.</summary>
    public void Finalize(DateTime utcNow)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(ChiefComplaint))
        {
            throw new DomainException("CONSULTATION_INCOMPLETE", "Record the chief complaint before finalizing.");
        }

        if (_diagnoses.Count(d => d.Type == DiagnosisType.Primary) != 1)
        {
            throw new DomainException("CONSULTATION_INCOMPLETE", "Record one primary diagnosis before finalizing.");
        }

        Status = ConsultationStatus.Finalized;
        FinalizedAt = utcNow;
    }

    /// <summary>Corrections or later notes on a finalized record; the original text stays unchanged.</summary>
    public ConsultationAddendum AddAddendum(string text)
    {
        if (Status != ConsultationStatus.Finalized)
        {
            throw new DomainException("CONSULTATION_NOT_FINALIZED", "Edit the draft directly; addenda are for finalized records.");
        }

        var addendum = ConsultationAddendum.Create(Id, text);
        _addenda.Add(addendum);
        return addendum;
    }

    private void EnsureDraft()
    {
        if (Status != ConsultationStatus.Draft)
        {
            throw new DomainException("CONSULTATION_FINALIZED", "This consultation is finalized and can no longer be changed. Add an addendum instead.");
        }
    }

    /// <summary>Updates lines with a known id, adds new ones and removes the rest, keeping the submitted order.</summary>
    private static void Sync<TItem, TValues>(List<TItem> items, IReadOnlyList<TValues> values, Func<TValues, Guid?> idOf,
        Func<TValues, int, TItem> create, Action<TItem, TValues, int> update) where TItem : BaseEntity
    {
        var keep = values.Where(v => idOf(v).HasValue).Select(v => idOf(v)!.Value).ToHashSet();
        var unknown = keep.Except(items.Select(i => i.Id)).ToList();
        if (unknown.Count > 0)
        {
            throw new DomainException("CONSULTATION_LINE_UNKNOWN", "One of the lines no longer exists. Reload the consultation and try again.");
        }

        items.RemoveAll(i => !keep.Contains(i.Id));
        for (var order = 0; order < values.Count; order++)
        {
            var v = values[order];
            if (idOf(v) is { } id)
            {
                update(items.First(i => i.Id == id), v, order);
            }
            else
            {
                items.Add(create(v, order));
            }
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[Audited]
public sealed class ConsultationDiagnosis : AuditableEntity
{
    private ConsultationDiagnosis() { }

    public Guid ConsultationId { get; private set; }
    public DiagnosisType Type { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    internal static ConsultationDiagnosis Create(Guid consultationId, DiagnosisType type, string description, int sortOrder)
    {
        var diagnosis = new ConsultationDiagnosis { ConsultationId = consultationId };
        diagnosis.Update(type, description, sortOrder);
        return diagnosis;
    }

    internal void Update(DiagnosisType type, string description, int sortOrder)
    {
        Type = type;
        Description = description.Trim();
        SortOrder = sortOrder;
    }
}

/// <summary>One medicine line. Free text in Phase 2; linked to the Pharmacy item master in Phase 6.</summary>
[Audited]
public sealed class PrescriptionItem : AuditableEntity
{
    private PrescriptionItem() { }

    public Guid ConsultationId { get; private set; }
    public int SortOrder { get; private set; }
    public string MedicineName { get; private set; } = string.Empty;
    public string? Form { get; private set; }
    public string? Strength { get; private set; }
    public string? Route { get; private set; }

    /// <summary>Morning + noon + night, e.g. "1+0+1" (common Bangladeshi notation).</summary>
    public string DosePattern { get; private set; } = string.Empty;
    public MedicineTiming Timing { get; private set; }
    public int? DurationValue { get; private set; }
    public DurationUnit DurationUnit { get; private set; }
    public int? Quantity { get; private set; }
    public string? Instructions { get; private set; }

    internal static PrescriptionItem Create(Guid consultationId, PrescriptionValues values, int sortOrder)
    {
        var item = new PrescriptionItem { ConsultationId = consultationId };
        item.Update(values, sortOrder);
        return item;
    }

    internal void Update(PrescriptionValues v, int sortOrder)
    {
        if (v.DurationUnit != DurationUnit.Continue && v.DurationValue is null or < 1)
        {
            throw new DomainException("PRESCRIPTION_DURATION", $"Enter the duration for {v.MedicineName.Trim()}.");
        }

        SortOrder = sortOrder;
        MedicineName = v.MedicineName.Trim();
        Form = Clean(v.Form);
        Strength = Clean(v.Strength);
        Route = Clean(v.Route);
        DosePattern = v.DosePattern.Trim();
        Timing = v.Timing;
        DurationUnit = v.DurationUnit;
        DurationValue = v.DurationUnit == DurationUnit.Continue ? null : v.DurationValue;
        Quantity = v.Quantity;
        Instructions = Clean(v.Instructions);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>A requested test. Free text in Phase 2; becomes a Laboratory/Radiology order in Phase 5.</summary>
[Audited]
public sealed class InvestigationRequest : AuditableEntity
{
    private InvestigationRequest() { }

    public Guid ConsultationId { get; private set; }
    public int SortOrder { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    internal static InvestigationRequest Create(Guid consultationId, string name, string? notes, int sortOrder)
    {
        var request = new InvestigationRequest { ConsultationId = consultationId };
        request.Update(name, notes, sortOrder);
        return request;
    }

    internal void Update(string name, string? notes, int sortOrder)
    {
        Name = name.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SortOrder = sortOrder;
    }
}

/// <summary>Append-only note on a finalized consultation (author and time come from the audit fields).</summary>
[Audited]
public sealed class ConsultationAddendum : AuditableEntity
{
    private ConsultationAddendum() { }

    public Guid ConsultationId { get; private set; }
    public string Text { get; private set; } = string.Empty;

    internal static ConsultationAddendum Create(Guid consultationId, string text) =>
        new() { ConsultationId = consultationId, Text = text.Trim() };
}
