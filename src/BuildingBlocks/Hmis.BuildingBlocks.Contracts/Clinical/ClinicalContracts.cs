namespace Hmis.BuildingBlocks.Contracts.Clinical;

// Contracts owned and implemented by the Clinical module.

/// <summary>
/// Standard ward frequencies. Fixed-time frequencies use the hospital's dose times (OD 9am, BD 9am/9pm, TDS 8am/2pm/10pm,
/// QID 6am/12pm/6pm/12am); interval frequencies repeat from the start time; STAT is one dose at the start; PRN has no schedule.
/// </summary>
public static class MedicationFrequencies
{
    public const string Od = "OD";
    public const string Bd = "BD";
    public const string Tds = "TDS";
    public const string Qid = "QID";
    public const string Q4H = "Q4H";
    public const string Q6H = "Q6H";
    public const string Q8H = "Q8H";
    public const string Q12H = "Q12H";
    public const string Stat = "STAT";
    public const string Prn = "PRN";

    public static readonly IReadOnlyList<string> All = [Od, Bd, Tds, Qid, Q4H, Q6H, Q8H, Q12H, Stat, Prn];
}

/// <summary>An inpatient medication order for the MAR. EndsAt is the stop time (or planned end); null means continuing.</summary>
public sealed record MedicationOrderInfo(
    Guid Id,
    Guid AdmissionId,
    Guid PatientId,
    string MedicineName,
    string? Strength,
    string? Form,
    string Route,
    string Dose,
    string Frequency,
    DateTime StartsAt,
    DateTime? EndsAt,
    string Status,
    string? PrnIndication,
    string? Instructions,
    Guid OrderedByDoctorId,
    DateTime OrderedAt);

public interface IMedicationOrderLookup
{
    Task<MedicationOrderInfo?> GetAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>All orders of the given admissions (active, stopped and completed), oldest first.</summary>
    Task<IReadOnlyList<MedicationOrderInfo>> GetForAdmissionsAsync(IReadOnlyCollection<Guid> admissionIds, CancellationToken cancellationToken);
}
