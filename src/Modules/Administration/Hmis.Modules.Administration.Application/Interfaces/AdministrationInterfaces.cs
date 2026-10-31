using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Domain.Entities;

namespace Hmis.Modules.Administration.Application.Interfaces;

public interface IAdministrationUnitOfWork : IUnitOfWork;

public interface IHospitalProfileRepository
{
    /// <summary>The single profile row (tracked), or null before seeding.</summary>
    Task<HospitalProfile?> GetAsync(CancellationToken cancellationToken);

    Task AddAsync(HospitalProfile profile, CancellationToken cancellationToken);

    void SetExpectedRowVersion(HospitalProfile profile, uint rowVersion);
}

public interface ISystemSettingsRepository
{
    Task<SystemSettings?> GetAsync(CancellationToken cancellationToken);

    Task AddAsync(SystemSettings settings, CancellationToken cancellationToken);

    void SetExpectedRowVersion(SystemSettings settings, uint rowVersion);
}

public interface IDepartmentRepository : IRepository<Department>, IConcurrencyRepository<Department>
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> HasActiveChildrenAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Department>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResponse<DepartmentDto>> SearchAsync(DepartmentListQuery query, CancellationToken cancellationToken);

    Task<DepartmentDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DepartmentLookupItemDto>> GetActiveLookupAsync(CancellationToken cancellationToken);
}

public interface IDesignationRepository : IRepository<Designation>, IConcurrencyRepository<Designation>
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken cancellationToken);

    Task<PagedResponse<DesignationDto>> SearchAsync(DesignationListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<Designation>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<DesignationLookupItemDto>> GetActiveLookupAsync(CancellationToken cancellationToken);
}

public sealed record AllocatedNumber(long Value, string Prefix, YearFormat YearFormat, int PadLength);

public interface INumberSequenceRepository : IConcurrencyRepository<NumberSequence>
{
    Task<NumberSequence?> GetByKeyAsync(string key, CancellationToken cancellationToken);

    Task<IReadOnlyList<NumberSequence>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(NumberSequence sequence, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically advances the counter (resetting it first when the yearly period changed) in one SQL statement
    /// and returns the allocated value with the format settings. Null if the key does not exist.
    /// </summary>
    Task<AllocatedNumber?> AllocateNextAsync(string key, int currentYear, CancellationToken cancellationToken);
}

public interface IHospitalProfileManager
{
    Task<HospitalProfileDto> GetAsync(CancellationToken cancellationToken);

    Task<HospitalProfileDto> UpdateAsync(HospitalProfileUpdateDto request, CancellationToken cancellationToken);

    Task<HospitalProfileDto> UploadLogoAsync(Stream content, long length, CancellationToken cancellationToken);

    Task<HospitalProfileDto> RemoveLogoAsync(CancellationToken cancellationToken);

    Task<BrandingDto> GetBrandingAsync(CancellationToken cancellationToken);

    Task<LogoFile?> OpenLogoAsync(CancellationToken cancellationToken);
}

public interface ISystemSettingsManager
{
    Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task<SystemSettingsDto> UpdateAsync(SystemSettingsUpdateDto request, CancellationToken cancellationToken);
}

public interface IDepartmentManager
{
    Task<PagedResponse<DepartmentDto>> SearchAsync(DepartmentListQuery query, CancellationToken cancellationToken);

    Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DepartmentLookupItemDto>> GetLookupAsync(CancellationToken cancellationToken);

    Task<DepartmentDto> CreateAsync(DepartmentCreateDto request, CancellationToken cancellationToken);

    Task<DepartmentDto> UpdateAsync(Guid id, DepartmentUpdateDto request, CancellationToken cancellationToken);

    Task<DepartmentDto> SetStatusAsync(Guid id, StatusUpdateDto request, CancellationToken cancellationToken);
}

public interface IDesignationManager
{
    Task<PagedResponse<DesignationDto>> SearchAsync(DesignationListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<DesignationLookupItemDto>> GetLookupAsync(CancellationToken cancellationToken);

    Task<DesignationDto> CreateAsync(DesignationCreateDto request, CancellationToken cancellationToken);

    Task<DesignationDto> UpdateAsync(Guid id, DesignationUpdateDto request, CancellationToken cancellationToken);

    Task<DesignationDto> SetStatusAsync(Guid id, StatusUpdateDto request, CancellationToken cancellationToken);
}

public interface INumberSequenceManager
{
    Task<IReadOnlyList<NumberSequenceDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<NumberSequenceDto> UpdateAsync(string key, NumberSequenceUpdateDto request, CancellationToken cancellationToken);

    Task<NumberSequenceDto> SetNextValueAsync(string key, NumberSequenceNextValueDto request, CancellationToken cancellationToken);
}
