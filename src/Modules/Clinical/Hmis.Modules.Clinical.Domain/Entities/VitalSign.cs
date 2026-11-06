using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;
using Hmis.BuildingBlocks.Domain.Exceptions;

namespace Hmis.Modules.Clinical.Domain.Entities;

public sealed record VitalValues(
    int? Systolic,
    int? Diastolic,
    int? Pulse,
    decimal? TemperatureF,
    int? RespiratoryRate,
    int? SpO2,
    decimal? WeightKg,
    decimal? HeightCm,
    string? Notes);

/// <summary>
/// One set of vital signs for an encounter, usually taken by a nurse before the consultation. Temperature is in °F
/// (common practice in Bangladesh). Never deleted: mistakes are marked as entered in error.
/// </summary>
[Audited]
public sealed class VitalSign : AuditableEntity, IAggregateRoot
{
    private VitalSign() { }

    public Guid PatientId { get; private set; }
    public EncounterType EncounterType { get; private set; }
    public Guid EncounterId { get; private set; }
    public int? Systolic { get; private set; }
    public int? Diastolic { get; private set; }
    public int? Pulse { get; private set; }
    public decimal? TemperatureF { get; private set; }
    public int? RespiratoryRate { get; private set; }
    public int? SpO2 { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? HeightCm { get; private set; }
    public string? Notes { get; private set; }
    public bool IsEnteredInError { get; private set; }
    public string? ErrorReason { get; private set; }

    /// <summary>Body mass index (kg/m²) when both weight and height are recorded.</summary>
    public decimal? Bmi => WeightKg is { } w && HeightCm is { } h && h > 0 ? Math.Round(w / (h / 100m * (h / 100m)), 1) : null;

    public static VitalSign Record(Guid patientId, EncounterType encounterType, Guid encounterId, VitalValues v)
    {
        if (v is { Systolic: null, Diastolic: null, Pulse: null, TemperatureF: null, RespiratoryRate: null, SpO2: null, WeightKg: null, HeightCm: null })
        {
            throw new DomainException("VITALS_EMPTY", "Record at least one measurement.");
        }

        if ((v.Systolic is null) != (v.Diastolic is null))
        {
            throw new DomainException("VITALS_BP_INCOMPLETE", "Enter both systolic and diastolic blood pressure.");
        }

        return new VitalSign
        {
            PatientId = patientId,
            EncounterType = encounterType,
            EncounterId = encounterId,
            Systolic = v.Systolic,
            Diastolic = v.Diastolic,
            Pulse = v.Pulse,
            TemperatureF = v.TemperatureF,
            RespiratoryRate = v.RespiratoryRate,
            SpO2 = v.SpO2,
            WeightKg = v.WeightKg,
            HeightCm = v.HeightCm,
            Notes = string.IsNullOrWhiteSpace(v.Notes) ? null : v.Notes.Trim()
        };
    }

    /// <summary>Only the user who recorded the vitals can void them (the same rule as nursing notes and the MAR).</summary>
    public void MarkEnteredInError(Guid userId, string reason)
    {
        if (CreatedBy != userId)
        {
            throw new DomainException("VITALS_NOT_YOURS", "Only the user who recorded these vitals can mark them as entered in error.");
        }

        if (IsEnteredInError)
        {
            throw new DomainException("VITALS_ALREADY_IN_ERROR", "These vitals are already marked as entered in error.");
        }

        IsEnteredInError = true;
        ErrorReason = reason.Trim();
    }
}
