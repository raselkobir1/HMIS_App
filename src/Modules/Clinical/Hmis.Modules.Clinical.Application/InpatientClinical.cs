using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.Modules.Clinical.Domain.Entities;

namespace Hmis.Modules.Clinical.Application;

// ---------- Inpatient DTOs (progress notes, medication orders, ward vitals) ----------

public sealed record InpatientStayDto(
    Guid AdmissionId, string AdmissionNumber, string Status, bool IsActive, DateTime AdmittedAt, DateTime? ReleasedAt, string? WardName,
    string? BedNumber, Guid AttendingDoctorId, string? AttendingDoctorName);

public sealed record ProgressNoteDto(
    Guid Id, ProgressNoteKind Kind, string? Subjective, string? Objective, string? Assessment, string? Plan, Guid DoctorId, string? DoctorName,
    DateTime WrittenAt, bool IsEnteredInError, string? ErrorReason);

public sealed record ProgressNoteCreateDto
{
    public ProgressNoteKind Kind { get; init; }
    public string? Subjective { get; init; }
    public string? Objective { get; init; }
    public string? Assessment { get; init; }
    public string? Plan { get; init; }
}

public sealed record MedicationOrderDto(
    Guid Id, string MedicineName, string? Strength, string? Form, string Route, string Dose, string Frequency, DateTime StartsAt,
    int? DurationDays, DateTime? EndsAt, string Status, string? PrnIndication, string? Instructions, string? OrderedByName, DateTime OrderedAt,
    DateTime? StoppedAt, string? StoppedByName, string? StopReason, uint RowVersion);

public sealed record MedicationOrderCreateDto
{
    public string MedicineName { get; init; } = string.Empty;
    public string? Strength { get; init; }
    public string? Form { get; init; }
    public string Route { get; init; } = string.Empty;
    public string Dose { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;

    /// <summary>Defaults to now.</summary>
    public DateTime? StartsAt { get; init; }

    /// <summary>Null means until stopped.</summary>
    public int? DurationDays { get; init; }

    public string? PrnIndication { get; init; }
    public string? Instructions { get; init; }
}

public sealed record MedicationOrderStopDto
{
    public string Reason { get; init; } = string.Empty;
    public uint RowVersion { get; init; }
}

/// <summary>The doctor's side of the inpatient chart.</summary>
public sealed record InpatientChartDto(
    InpatientStayDto Stay,
    PatientInfoDto Patient,
    IReadOnlyList<AllergyAlertDto> Allergies,
    IReadOnlyList<ProgressNoteDto> ProgressNotes,
    IReadOnlyList<MedicationOrderDto> MedicationOrders,
    IReadOnlyList<VitalSignDto> Vitals,
    // The signed-in user is linked to a doctor profile (can sign notes and orders with Clinical.Inpatient.Write).
    bool IsDoctor,
    Guid? CurrentDoctorId);

// ---------- Inpatient repositories and manager ----------

public interface IProgressNoteRepository : IRepository<ProgressNote>
{
    Task<IReadOnlyList<ProgressNote>> GetForAdmissionAsync(Guid admissionId, CancellationToken cancellationToken);
}

public interface IMedicationOrderRepository : IRepository<MedicationOrder>, IConcurrencyRepository<MedicationOrder>
{
    Task<IReadOnlyList<MedicationOrder>> GetForAdmissionsAsync(IReadOnlyCollection<Guid> admissionIds, CancellationToken cancellationToken);
}

public interface IInpatientClinicalManager
{
    /// <summary>Notes, orders and vitals for one stay. Access is audited.</summary>
    Task<InpatientChartDto> GetChartAsync(Guid admissionId, CancellationToken cancellationToken);

    Task<ProgressNoteDto> AddProgressNoteAsync(Guid admissionId, ProgressNoteCreateDto request, CancellationToken cancellationToken);

    Task<ProgressNoteDto> MarkProgressNoteInErrorAsync(Guid noteId, EnteredInErrorDto request, CancellationToken cancellationToken);

    Task<MedicationOrderDto> CreateOrderAsync(Guid admissionId, MedicationOrderCreateDto request, CancellationToken cancellationToken);

    Task<MedicationOrderDto> StopOrderAsync(Guid orderId, MedicationOrderStopDto request, CancellationToken cancellationToken);

    Task<VitalSignDto> RecordVitalsAsync(Guid admissionId, VitalSignRequestDto request, CancellationToken cancellationToken);
}
