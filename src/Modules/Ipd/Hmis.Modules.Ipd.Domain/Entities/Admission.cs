using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Ipd.Domain.Entities;

/// <summary>
/// Admitted → DischargeOrdered (doctor) → FinalBilled (charges cut off, bill finalized) → Discharged (bed released).
/// Billing never blocks the doctor's discharge order; only the release needs a settled bill or an administrator's override.
/// </summary>
public enum AdmissionStatus
{
    Admitted,
    DischargeOrdered,
    FinalBilled,
    Discharged
}

public enum AdmissionSource
{
    Direct,
    Opd,
    Emergency,
    Referral
}

public enum DischargeType
{
    Normal,
    OnRequest,
    LeftAgainstMedicalAdvice,
    Referred,
    Death
}

public sealed record EmergencyContactValues(string? Name, string? Relation, string? Phone);

public sealed record DischargeValues(
    DischargeType Type,
    string FinalDiagnosis,
    string? HospitalCourse,
    string? DischargeMedications,
    string? Advice,
    DateOnly? FollowUpDate);

[Audited]
public sealed class Admission : AuditableEntity, IAggregateRoot, IHasConcurrencyToken
{
    private readonly List<BedAssignment> _beds = [];

    private Admission() { }

    public string AdmissionNumber { get; private set; } = string.Empty;
    public Guid PatientId { get; private set; }
    public Guid AttendingDoctorId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public AdmissionSource Source { get; private set; }
    public string? SourceReference { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactRelation { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public DateTime AdmittedAt { get; private set; }
    public AdmissionStatus Status { get; private set; } = AdmissionStatus.Admitted;

    public Guid? DischargeOrderedBy { get; private set; }
    public DateTime? DischargeOrderedAt { get; private set; }
    public DischargeType? DischargeType { get; private set; }
    public string? FinalDiagnosis { get; private set; }
    public string? HospitalCourse { get; private set; }
    public string? DischargeMedications { get; private set; }
    public string? DischargeAdvice { get; private set; }
    public DateOnly? FollowUpDate { get; private set; }

    /// <summary>
    /// Discharge date and time for billing: the doctor's discharge order time (Q-IPD-1, the day is free when the order is
    /// before noon). Bed charges stop here and the bill is finalized. The moment the patient actually left is ReleasedAt.
    /// </summary>
    public DateTime? DischargedAt { get; private set; }

    /// <summary>The patient left and the bed was freed.</summary>
    public DateTime? ReleasedAt { get; private set; }

    public decimal? ReleasedWithDue { get; private set; }
    public string? ReleaseOverrideReason { get; private set; }
    public Guid? ReleaseOverrideBy { get; private set; }

    /// <summary>
    /// Bed days priced at or before this instant are on a finalized final bill. Kept when a stay is reopened so the days
    /// posted afterwards (to a supplementary bill) start after it.
    /// </summary>
    public DateTime? FinalBilledUntil { get; private set; }

    /// <summary>An administrator moved the stay back from FinalBilled to Admitted (e.g. the patient could not leave).</summary>
    public DateTime? ReopenedAt { get; private set; }

    public Guid? ReopenedBy { get; private set; }
    public string? ReopenReason { get; private set; }

    /// <summary>Incremented on every change so the RowVersion check also covers bed history changes.</summary>
    public int Revision { get; private set; }

    public uint RowVersion { get; private set; }

    public IReadOnlyList<BedAssignment> Beds => _beds;

    public BedAssignment? CurrentBed => _beds.FirstOrDefault(b => b.EndedAt is null);

    public bool IsActive => Status != AdmissionStatus.Discharged;

    public static Admission Admit(string admissionNumber, Guid patientId, Guid attendingDoctorId, Guid? departmentId, AdmissionSource source,
        string? sourceReference, string reason, EmergencyContactValues contact, Bed bed, DateTime admittedAt)
    {
        var admission = new Admission
        {
            AdmissionNumber = admissionNumber,
            PatientId = patientId,
            AttendingDoctorId = attendingDoctorId,
            DepartmentId = departmentId,
            Source = source,
            SourceReference = Clean(sourceReference),
            Reason = reason.Trim(),
            EmergencyContactName = Clean(contact.Name),
            EmergencyContactRelation = Clean(contact.Relation),
            EmergencyContactPhone = Clean(contact.Phone),
            AdmittedAt = admittedAt
        };
        bed.Occupy();
        admission._beds.Add(BedAssignment.Start(admission.Id, bed.Id, admittedAt, "Admission"));
        return admission;
    }

    public void TransferBed(Bed current, Bed target, string reason, DateTime at)
    {
        EnsureInBed();
        if (target.Id == current.Id || CurrentBed?.BedId != current.Id)
        {
            throw new DomainException("ADMISSION_TRANSFER_INVALID", "Choose a different bed to transfer to.");
        }

        target.Occupy();
        CurrentBed!.End(at);
        current.Vacate();
        _beds.Add(BedAssignment.Start(Id, target.Id, at, reason));
        Revision++;
    }

    public void ChangeAttendingDoctor(Guid doctorId, Guid? departmentId)
    {
        EnsureInBed();
        AttendingDoctorId = doctorId;
        DepartmentId = departmentId;
        Revision++;
    }

    /// <summary>The doctor's discharge order with the discharge summary. Can be updated until the patient leaves.</summary>
    public void OrderDischarge(Guid doctorId, DischargeValues values, DateTime at)
    {
        if (Status is AdmissionStatus.Discharged)
        {
            throw new DomainException("ADMISSION_DISCHARGED", "The patient has already been discharged.");
        }

        if (Status == AdmissionStatus.Admitted)
        {
            Status = AdmissionStatus.DischargeOrdered;
            DischargeOrderedAt = at;
            DischargeOrderedBy = doctorId;
        }

        DischargeType = values.Type;
        FinalDiagnosis = values.FinalDiagnosis.Trim();
        HospitalCourse = Clean(values.HospitalCourse);
        DischargeMedications = Clean(values.DischargeMedications);
        DischargeAdvice = Clean(values.Advice);
        FollowUpDate = values.FollowUpDate;
        Revision++;
    }

    /// <summary>The patient stays after all (e.g. condition changed). Only before the final bill.</summary>
    public void CancelDischargeOrder()
    {
        Require("Cancel the discharge order only before the final bill is prepared.", AdmissionStatus.DischargeOrdered);
        Status = AdmissionStatus.Admitted;
        DischargeOrderedAt = null;
        DischargeOrderedBy = null;
        Revision++;
    }

    /// <summary>Fixes the discharge time (the discharge order time); bed charges stop here and the bill is finalized.</summary>
    public void MarkFinalBilled(DateTime dischargedAt)
    {
        Require("The doctor must order the discharge before the final bill.", AdmissionStatus.DischargeOrdered);
        Status = AdmissionStatus.FinalBilled;
        DischargedAt = dischargedAt;
        FinalBilledUntil = dischargedAt;
        Revision++;
    }

    /// <summary>
    /// Administrator's correction after the final bill: the patient stays after all. The discharge order is cancelled (the
    /// summary text is kept as a draft), the finalized bill stays payable and later charges go to a supplementary bill.
    /// </summary>
    public void Reopen(Guid userId, string reason, DateTime at)
    {
        Require("Only a stay with a prepared final bill can be reopened.", AdmissionStatus.FinalBilled);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("ADMISSION_REOPEN_REASON", "Give the reason for reopening the stay.");
        }

        Status = AdmissionStatus.Admitted;
        DischargedAt = null;
        DischargeOrderedAt = null;
        DischargeOrderedBy = null;
        ReopenedAt = at;
        ReopenedBy = userId;
        ReopenReason = reason.Trim();
        Revision++;
    }

    /// <summary>
    /// The instant bed charges run to: the discharge order time once discharge is ordered (the patient is leaving; a day
    /// ordered before noon is free), otherwise now. The final bill uses the same cut-off.
    /// </summary>
    public DateTime ChargeCutOff(DateTime now) =>
        Status == AdmissionStatus.DischargeOrdered && DischargeOrderedAt is { } ordered && ordered < now ? ordered : now;

    /// <summary>The patient leaves and the bed is freed. <paramref name="due"/> is non-zero only with an override.</summary>
    public void Release(Bed bed, DateTime at, decimal due, Guid? overrideBy, string? overrideReason)
    {
        Require("Prepare the final bill before releasing the patient.", AdmissionStatus.FinalBilled);
        if (CurrentBed?.BedId != bed.Id)
        {
            throw new DomainException("ADMISSION_TRANSFER_INVALID", "The bed does not match the patient's current bed.");
        }

        CurrentBed!.End(at);
        bed.Vacate();
        Status = AdmissionStatus.Discharged;
        ReleasedAt = at;
        if (due > 0)
        {
            ReleasedWithDue = due;
            ReleaseOverrideBy = overrideBy;
            ReleaseOverrideReason = overrideReason?.Trim();
        }

        Revision++;
    }

    /// <summary>The bed held at an instant (used to price each bed day).</summary>
    public Guid? BedAt(DateTime instant) =>
        _beds.Where(b => b.StartedAt <= instant && (b.EndedAt is null || b.EndedAt > instant)).Select(b => (Guid?)b.BedId).FirstOrDefault()
        ?? _beds.OrderBy(b => b.StartedAt).LastOrDefault(b => b.StartedAt <= instant)?.BedId;

    private void EnsureInBed()
    {
        Require("This is only possible while the patient is admitted.", AdmissionStatus.Admitted, AdmissionStatus.DischargeOrdered);
    }

    private void Require(string message, params AdmissionStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException("ADMISSION_INVALID_STATUS", message);
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>One stay in one bed. Never deleted; transfers end the current row and start a new one.</summary>
[Audited]
public sealed class BedAssignment : AuditableEntity
{
    private BedAssignment() { }

    public Guid AdmissionId { get; private set; }
    public Guid BedId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    internal static BedAssignment Start(Guid admissionId, Guid bedId, DateTime at, string reason) =>
        new() { AdmissionId = admissionId, BedId = bedId, StartedAt = at, Reason = reason.Trim() };

    internal void End(DateTime at) => EndedAt = at;
}
