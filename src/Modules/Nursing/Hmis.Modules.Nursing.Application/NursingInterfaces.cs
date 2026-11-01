using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.Modules.Nursing.Domain.Entities;

namespace Hmis.Modules.Nursing.Application;

public interface INursingUnitOfWork : IUnitOfWork;

public interface IMedicationAdministrationRepository : IRepository<MedicationAdministration>
{
    /// <summary>Untracked: entries of the given admissions, scheduled in or occurring in [fromUtc, toUtc).</summary>
    Task<IReadOnlyList<MedicationAdministration>> GetForAdmissionsAsync(
        IReadOnlyCollection<Guid> admissionIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    /// <summary>A valid (not in error) entry for the scheduled dose exists.</summary>
    Task<bool> IsRecordedAsync(Guid orderId, DateTime scheduledAt, CancellationToken cancellationToken);
}

public interface IIntakeOutputRepository : IRepository<IntakeOutputEntry>
{
    Task<IReadOnlyList<IntakeOutputEntry>> GetForAdmissionAsync(Guid admissionId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

public interface INursingNoteRepository : IRepository<NursingNote>
{
    Task<IReadOnlyList<NursingNote>> GetForAdmissionAsync(Guid admissionId, CancellationToken cancellationToken);
}

public interface IShiftHandoverRepository : IRepository<ShiftHandover>, IConcurrencyRepository<ShiftHandover>
{
    /// <summary>Tracked, with items.</summary>
    Task<ShiftHandover?> GetWithItemsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked, with items.</summary>
    Task<ShiftHandover?> FindAsync(Guid wardId, DateOnly date, Shift shift, CancellationToken cancellationToken);

    /// <summary>Untracked, with items: the ward's handovers from the given dates.</summary>
    Task<IReadOnlyList<ShiftHandover>> GetForWardAsync(Guid wardId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public interface INurseAssignmentRepository : IRepository<NurseAssignment>
{
    /// <summary>Untracked, ordered by date, shift and ward.</summary>
    Task<IReadOnlyList<NurseAssignment>> SearchAsync(DateOnly from, DateOnly to, Guid? wardId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NurseAssignment>> GetForShiftAsync(DateOnly date, Shift shift, CancellationToken cancellationToken);
}

public interface IWardNursingManager
{
    Task<CurrentShiftDto> GetCurrentShiftAsync(CancellationToken cancellationToken);

    /// <summary>Patients in the ward with doses due and overdue, and the nurses on duty now.</summary>
    Task<WardRoundDto> GetWardAsync(Guid wardId, CancellationToken cancellationToken);

    Task<NursingPatientDto> GetPatientAsync(Guid admissionId, CancellationToken cancellationToken);
}

public interface IMarManager
{
    /// <summary>The MAR for one hospital-local day (midnight to midnight). Defaults to today.</summary>
    Task<MarDto> GetAsync(Guid admissionId, DateOnly? date, CancellationToken cancellationToken);

    Task<AdministrationDto> RecordAsync(Guid admissionId, AdministrationCreateDto request, CancellationToken cancellationToken);

    Task<AdministrationDto> MarkInErrorAsync(Guid id, EnteredInErrorDto request, CancellationToken cancellationToken);
}

public interface INursingRecordManager
{
    /// <summary>One fluid day (8am to 8am). Defaults to the current fluid day.</summary>
    Task<IntakeOutputDayDto> GetIntakeOutputAsync(Guid admissionId, DateOnly? date, CancellationToken cancellationToken);

    Task<IntakeOutputEntryDto> RecordIntakeOutputAsync(Guid admissionId, IntakeOutputCreateDto request, CancellationToken cancellationToken);

    Task<IntakeOutputEntryDto> MarkIntakeOutputInErrorAsync(Guid id, EnteredInErrorDto request, CancellationToken cancellationToken);

    Task<NursingNotesDto> GetNotesAsync(Guid admissionId, CancellationToken cancellationToken);

    Task<NursingNoteDto> AddNoteAsync(Guid admissionId, NursingNoteCreateDto request, CancellationToken cancellationToken);

    Task<NursingNoteDto> MarkNoteInErrorAsync(Guid id, EnteredInErrorDto request, CancellationToken cancellationToken);
}

public interface IShiftManager
{
    /// <summary>The ward's handovers for a date (and the night before it). Defaults to today.</summary>
    Task<IReadOnlyList<HandoverDto>> GetHandoversAsync(HandoverQuery query, CancellationToken cancellationToken);

    /// <summary>Creates the ward's handover for the shift, or revises the author's own unreceived one.</summary>
    Task<HandoverDto> SaveHandoverAsync(HandoverSaveDto request, CancellationToken cancellationToken);

    Task<HandoverDto> ReceiveHandoverAsync(Guid id, HandoverReceiveDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<RosterEntryDto>> GetRosterAsync(RosterQuery query, CancellationToken cancellationToken);

    Task<RosterEntryDto> AddRosterEntryAsync(RosterCreateDto request, CancellationToken cancellationToken);

    Task RemoveRosterEntryAsync(Guid id, CancellationToken cancellationToken);
}
