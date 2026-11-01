using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Ipd.Domain.Entities;

namespace Hmis.Modules.Ipd.Application;

public interface IIpdUnitOfWork : IUnitOfWork;

public interface IBedCategoryRepository : IRepository<BedCategory>, IConcurrencyRepository<BedCategory>
{
    Task<IReadOnlyList<BedCategory>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BedCategory>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public interface IWardRepository : IRepository<Ward>, IConcurrencyRepository<Ward>
{
    Task<IReadOnlyList<Ward>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Ward>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
}

public interface IBedRepository : IRepository<Bed>, IConcurrencyRepository<Bed>
{
    /// <summary>Untracked, ordered by ward and bed number.</summary>
    Task<IReadOnlyList<Bed>> SearchAsync(BedQuery query, CancellationToken cancellationToken);

    /// <summary>Untracked.</summary>
    Task<IReadOnlyList<Bed>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> NumberExistsAsync(Guid wardId, string bedNumber, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> CountByWardAsync(CancellationToken cancellationToken);
}

public interface IAdmissionRepository : IRepository<Admission>, IConcurrencyRepository<Admission>
{
    /// <summary>Tracked, with bed history.</summary>
    Task<Admission?> GetWithBedsAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResponse<Admission>> SearchAsync(AdmissionListQuery query, CancellationToken cancellationToken);

    Task<bool> HasActiveAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>Untracked, with bed history: patients still in a bed (admitted or discharge ordered).</summary>
    Task<IReadOnlyList<Admission>> GetInBedAsync(CancellationToken cancellationToken);

    /// <summary>Untracked, with bed history: every patient not yet discharged (including final-billed patients still in a bed).</summary>
    Task<IReadOnlyList<Admission>> GetActiveAsync(CancellationToken cancellationToken);

    Task<bool> IsAttendingDoctorAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken);

    /// <summary>Untracked: the active admission occupying each of the given beds.</summary>
    Task<IReadOnlyList<Admission>> GetActiveForBedsAsync(IReadOnlyCollection<Guid> bedIds, CancellationToken cancellationToken);
}

public interface IBedSetupManager
{
    Task<IReadOnlyList<BedCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<BedCategoryDto> CreateCategoryAsync(BedCategorySaveDto request, CancellationToken cancellationToken);

    Task<BedCategoryDto> UpdateCategoryAsync(Guid id, BedCategorySaveDto request, CancellationToken cancellationToken);

    Task<BedCategoryDto> SetCategoryActiveAsync(Guid id, ActiveDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<WardDto>> GetWardsAsync(CancellationToken cancellationToken);

    Task<WardDto> CreateWardAsync(WardSaveDto request, CancellationToken cancellationToken);

    Task<WardDto> UpdateWardAsync(Guid id, WardSaveDto request, CancellationToken cancellationToken);

    Task<WardDto> SetWardActiveAsync(Guid id, ActiveDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BedDto>> GetBedsAsync(BedQuery query, CancellationToken cancellationToken);

    Task<BedDto> CreateBedAsync(BedSaveDto request, CancellationToken cancellationToken);

    Task<BedDto> UpdateBedAsync(Guid id, BedSaveDto request, CancellationToken cancellationToken);

    Task<BedDto> SetBedActiveAsync(Guid id, ActiveDto request, CancellationToken cancellationToken);

    Task<BedDto> SetBedStatusAsync(Guid id, BedStatusDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<WardBoardDto>> GetBoardAsync(CancellationToken cancellationToken);
}

public interface IAdmissionManager
{
    Task<PagedResponse<AdmissionSummaryDto>> SearchAsync(AdmissionListQuery query, CancellationToken cancellationToken);

    Task<AdmissionDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AdmissionDto> AdmitAsync(AdmitDto request, CancellationToken cancellationToken);

    Task<AdmissionDto> TransferAsync(Guid id, TransferDto request, CancellationToken cancellationToken);

    Task<AdmissionDto> ChangeDoctorAsync(Guid id, ChangeDoctorDto request, CancellationToken cancellationToken);

    Task<AdmissionDto> SyncChargesAsync(Guid id, CancellationToken cancellationToken);

    Task<AdmissionDto> OrderDischargeAsync(Guid id, DischargeOrderDto request, CancellationToken cancellationToken);

    Task<AdmissionDto> CancelDischargeOrderAsync(Guid id, AdmissionRowVersionDto request, CancellationToken cancellationToken);

    Task<AdmissionDto> PrepareFinalBillAsync(Guid id, AdmissionRowVersionDto request, CancellationToken cancellationToken);

    Task<AdmissionDto> ReleaseAsync(Guid id, ReleaseDto request, CancellationToken cancellationToken);

    Task<DischargeSummaryDto> GetDischargeSummaryAsync(Guid id, CancellationToken cancellationToken);
}
