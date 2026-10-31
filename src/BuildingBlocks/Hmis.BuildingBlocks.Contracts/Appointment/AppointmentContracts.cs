namespace Hmis.BuildingBlocks.Contracts.Appointment;

// Contracts owned and implemented by the Appointment module.

/// <summary>
/// An outpatient encounter. In Phase 2 the OPD visit is the checked-in appointment (ARCHITECTURE.md D16):
/// Id is the appointment id, Number the appointment number.
/// </summary>
public sealed record OpdEncounter(
    Guid Id,
    string Number,
    Guid PatientId,
    Guid DoctorId,
    DateOnly Date,
    int SerialNumber,
    string VisitType,
    string Status);

/// <summary>Lets Clinical open and close OPD consultations without owning the queue.</summary>
public interface IOpdEncounterService
{
    Task<OpdEncounter?> GetAsync(Guid encounterId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a waiting patient into consultation with the signed-in doctor (no-op when already in consultation).
    /// Throws when the signed-in user is not the encounter's doctor or another consultation is open.
    /// </summary>
    Task<OpdEncounter> StartAsync(Guid encounterId, CancellationToken cancellationToken);

    /// <summary>Completes the encounter (no-op when already completed).</summary>
    Task<OpdEncounter> CompleteAsync(Guid encounterId, CancellationToken cancellationToken);

    /// <summary>True when the doctor has (or had) a non-cancelled appointment with the patient.</summary>
    Task<bool> HasRelationshipAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken);
}
