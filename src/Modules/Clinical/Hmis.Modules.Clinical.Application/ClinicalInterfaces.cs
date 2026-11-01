using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.Modules.Clinical.Domain.Entities;

namespace Hmis.Modules.Clinical.Application;

public interface IClinicalUnitOfWork : IUnitOfWork;

public interface IConsultationRepository : IRepository<Consultation>, IConcurrencyRepository<Consultation>
{
    /// <summary>Loads the consultation with all its lines (tracked).</summary>
    Task<Consultation?> GetWithLinesAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads the encounter's consultation with all its lines (tracked).</summary>
    Task<Consultation?> GetByEncounterAsync(EncounterType encounterType, Guid encounterId, CancellationToken cancellationToken);

    /// <summary>Consultations of the given patient records, newest first, with diagnoses and prescription lines (read-only).</summary>
    Task<IReadOnlyList<Consultation>> GetForPatientAsync(IReadOnlyCollection<Guid> patientIds, int take, CancellationToken cancellationToken);
}

public interface IVitalSignRepository : IRepository<VitalSign>
{
    Task<IReadOnlyList<VitalSign>> GetForEncounterAsync(EncounterType encounterType, Guid encounterId, CancellationToken cancellationToken);
}

public interface IClinicalManager
{
    Task<OpdWorkspaceDto> GetOpdWorkspaceAsync(Guid encounterId, CancellationToken cancellationToken);

    Task<VitalSignDto> RecordOpdVitalsAsync(Guid encounterId, VitalSignRequestDto request, CancellationToken cancellationToken);

    Task<VitalSignDto> MarkVitalsInErrorAsync(Guid vitalSignId, EnteredInErrorDto request, CancellationToken cancellationToken);

    /// <summary>Starts the consultation for the encounter's doctor (idempotent: returns the existing one).</summary>
    Task<ConsultationDto> OpenOpdConsultationAsync(Guid encounterId, CancellationToken cancellationToken);

    Task<ConsultationRecordDto> GetConsultationAsync(Guid id, CancellationToken cancellationToken);

    Task<ConsultationDto> SaveDraftAsync(Guid id, ConsultationSaveDto request, CancellationToken cancellationToken);

    /// <summary>Finalizes the record and completes the encounter (safe to repeat if the second step failed).</summary>
    Task<ConsultationDto> FinalizeAsync(Guid id, FinalizeDto request, CancellationToken cancellationToken);

    Task<ConsultationDto> AddAddendumAsync(Guid id, AddendumCreateDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsultationSummaryDto>> GetPatientHistoryAsync(Guid patientId, CancellationToken cancellationToken);
}
