using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Patient;
using Hmis.Modules.Ipd.Domain.Entities;

namespace Hmis.Modules.Ipd.Application;

/// <summary>Bed categories (rates), wards, beds, housekeeping statuses and the bed board.</summary>
public sealed class BedSetupManager(
    IBedCategoryRepository categories,
    IWardRepository wards,
    IBedRepository beds,
    IAdmissionRepository admissions,
    IIpdUnitOfWork unitOfWork,
    IDepartmentLookup departmentLookup,
    IPatientLookup patientLookup,
    IDoctorLookup doctorLookup) : IBedSetupManager
{
    // ---------- Bed categories ----------

    public async Task<IReadOnlyList<BedCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        (await categories.GetAllAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<BedCategoryDto> CreateCategoryAsync(BedCategorySaveDto request, CancellationToken cancellationToken)
    {
        var category = BedCategory.Create(request.Name, request.DailyRate, request.SuggestedDeposit);
        await categories.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public async Task<BedCategoryDto> UpdateCategoryAsync(Guid id, BedCategorySaveDto request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bed category", id);
        categories.SetExpectedRowVersion(category, request.RowVersion);
        category.Update(request.Name, request.DailyRate, request.SuggestedDeposit);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public async Task<BedCategoryDto> SetCategoryActiveAsync(Guid id, ActiveDto request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bed category", id);
        category.SetActive(request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    // ---------- Wards ----------

    public async Task<IReadOnlyList<WardDto>> GetWardsAsync(CancellationToken cancellationToken)
    {
        var list = await wards.GetAllAsync(cancellationToken);
        var counts = await beds.CountByWardAsync(cancellationToken);
        var departments = await DepartmentNamesAsync(list.Select(w => w.DepartmentId), cancellationToken);
        return list.Select(w => ToDto(w, departments, counts.GetValueOrDefault(w.Id))).ToList();
    }

    public async Task<WardDto> CreateWardAsync(WardSaveDto request, CancellationToken cancellationToken)
    {
        if (await wards.CodeExistsAsync(request.Code.Trim().ToUpperInvariant(), cancellationToken))
        {
            throw new ConflictException("Another ward already uses this code.", IpdErrorCodes.WardCodeExists);
        }

        await EnsureDepartmentAsync(request.DepartmentId, cancellationToken);
        var ward = Ward.Create(request.Code, request.Name, request.Type, request.DepartmentId, request.Building, request.Floor);
        await wards.AddAsync(ward, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(ward, await DepartmentNamesAsync([ward.DepartmentId], cancellationToken), 0);
    }

    public async Task<WardDto> UpdateWardAsync(Guid id, WardSaveDto request, CancellationToken cancellationToken)
    {
        var ward = await GetWardAsync(id, cancellationToken);
        wards.SetExpectedRowVersion(ward, request.RowVersion);
        await EnsureDepartmentAsync(request.DepartmentId, cancellationToken);
        ward.Update(request.Name, request.Type, request.DepartmentId, request.Building, request.Floor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await WardDtoAsync(ward, cancellationToken);
    }

    public async Task<WardDto> SetWardActiveAsync(Guid id, ActiveDto request, CancellationToken cancellationToken)
    {
        var ward = await GetWardAsync(id, cancellationToken);
        if (!request.IsActive && (await beds.SearchAsync(new BedQuery { WardId = id }, cancellationToken)).Any(b => b.Status == BedStatus.Occupied))
        {
            throw new BusinessException("WARD_OCCUPIED", "Patients are in this ward. Transfer or discharge them before closing it.");
        }

        ward.SetActive(request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await WardDtoAsync(ward, cancellationToken);
    }

    // ---------- Beds ----------

    public async Task<IReadOnlyList<BedDto>> GetBedsAsync(BedQuery query, CancellationToken cancellationToken)
    {
        var list = await beds.SearchAsync(query, cancellationToken);
        var (wardMap, categoryMap) = await SetupMapsAsync(cancellationToken);
        return list.Select(b => ToDto(b, wardMap, categoryMap)).ToList();
    }

    public async Task<BedDto> CreateBedAsync(BedSaveDto request, CancellationToken cancellationToken)
    {
        var ward = await wards.GetByIdAsync(request.WardId, cancellationToken);
        if (ward is not { IsActive: true })
        {
            throw new BusinessException(IpdErrorCodes.WardInvalid, "Select an active ward.", "wardId");
        }

        await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        await EnsureBedNumberFreeAsync(request.WardId, request.BedNumber, null, cancellationToken);
        var bed = Bed.Create(request.WardId, request.BedNumber, request.Room, request.CategoryId);
        await beds.AddAsync(bed, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BedDtoAsync(bed, cancellationToken);
    }

    /// <summary>Changing the category changes the rate for bed days not yet charged; days already on the bill keep their price.</summary>
    public async Task<BedDto> UpdateBedAsync(Guid id, BedSaveDto request, CancellationToken cancellationToken)
    {
        var bed = await GetBedAsync(id, cancellationToken);
        beds.SetExpectedRowVersion(bed, request.RowVersion);
        await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        await EnsureBedNumberFreeAsync(bed.WardId, request.BedNumber, bed.Id, cancellationToken);
        bed.Update(request.BedNumber, request.Room, request.CategoryId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BedDtoAsync(bed, cancellationToken);
    }

    public async Task<BedDto> SetBedActiveAsync(Guid id, ActiveDto request, CancellationToken cancellationToken)
    {
        var bed = await GetBedAsync(id, cancellationToken);
        bed.SetActive(request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BedDtoAsync(bed, cancellationToken);
    }

    public async Task<BedDto> SetBedStatusAsync(Guid id, BedStatusDto request, CancellationToken cancellationToken)
    {
        var bed = await GetBedAsync(id, cancellationToken);
        bed.SetStatus(request.Status, request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BedDtoAsync(bed, cancellationToken);
    }

    // ---------- Board ----------

    /// <summary>Every active ward with its beds; occupied beds show the patient, doctor and admission.</summary>
    public async Task<IReadOnlyList<WardBoardDto>> GetBoardAsync(CancellationToken cancellationToken)
    {
        var wardList = (await wards.GetAllAsync(cancellationToken)).Where(w => w.IsActive).ToList();
        var bedList = (await beds.SearchAsync(new BedQuery(), cancellationToken)).Where(b => b.IsActive).ToList();
        var categoryMap = (await categories.GetAllAsync(cancellationToken)).ToDictionary(c => c.Id);
        var occupied = bedList.Where(b => b.Status == BedStatus.Occupied).Select(b => b.Id).ToList();
        var stays = await admissions.GetActiveForBedsAsync(occupied, cancellationToken);
        var byBed = stays.Where(a => a.CurrentBed is not null).ToDictionary(a => a.CurrentBed!.BedId);
        var patients = (await patientLookup.GetManyAsync(stays.Select(a => a.PatientId).Distinct().ToList(), cancellationToken)).ToDictionary(p => p.Id);
        var doctors = (await doctorLookup.GetManyAsync(stays.Select(a => a.AttendingDoctorId).Distinct().ToList(), cancellationToken))
            .ToDictionary(d => d.DoctorId);

        return wardList.Select(w =>
        {
            var wardBeds = bedList.Where(b => b.WardId == w.Id).Select(b =>
            {
                var category = categoryMap.GetValueOrDefault(b.CategoryId);
                OccupantDto? occupant = null;
                if (byBed.TryGetValue(b.Id, out var a))
                {
                    var p = patients.GetValueOrDefault(a.PatientId);
                    occupant = new OccupantDto(a.Id, a.AdmissionNumber, a.PatientId, p?.FullName, p?.MRNumber, p?.Gender,
                        doctors.GetValueOrDefault(a.AttendingDoctorId)?.FullName, a.AdmittedAt, a.Status);
                }

                return new BedBoardDto(b.Id, b.BedNumber, b.Room, category?.Name, category?.DailyRate ?? 0, category?.SuggestedDeposit ?? 0,
                    b.Status, b.StatusNote, b.IsActive, occupant);
            }).ToList();

            return new WardBoardDto(w.Id, w.Code, w.Name, w.Type, w.Building, w.Floor,
                wardBeds.Count(b => b.Status == BedStatus.Available), wardBeds.Count(b => b.Status == BedStatus.Occupied),
                wardBeds.Count(b => b.Status is not (BedStatus.Available or BedStatus.Occupied)), wardBeds);
        }).ToList();
    }

    // ---------- helpers ----------

    private async Task EnsureDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken)
    {
        if (departmentId is { } id && await departmentLookup.GetAsync(id, cancellationToken) is not { IsActive: true })
        {
            throw new BusinessException("IPD_DEPARTMENT_INVALID", "Select an active department.", "departmentId");
        }
    }

    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (await categories.GetByIdAsync(categoryId, cancellationToken) is not { IsActive: true })
        {
            throw new BusinessException(IpdErrorCodes.CategoryInvalid, "Select an active bed category.", "categoryId");
        }
    }

    private async Task EnsureBedNumberFreeAsync(Guid wardId, string bedNumber, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await beds.NumberExistsAsync(wardId, bedNumber.Trim().ToUpperInvariant(), excludeId, cancellationToken))
        {
            throw new ConflictException("This ward already has a bed with this number.", IpdErrorCodes.BedNumberExists);
        }
    }

    private async Task<Ward> GetWardAsync(Guid id, CancellationToken cancellationToken) =>
        await wards.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Ward", id);

    private async Task<Bed> GetBedAsync(Guid id, CancellationToken cancellationToken) =>
        await beds.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bed", id);

    private async Task<IReadOnlyDictionary<Guid, string>> DepartmentNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return list.Count == 0
            ? new Dictionary<Guid, string>()
            : (await departmentLookup.GetManyAsync(list, cancellationToken)).ToDictionary(d => d.Id, d => d.Name);
    }

    private async Task<(IReadOnlyDictionary<Guid, Ward>, IReadOnlyDictionary<Guid, BedCategory>)> SetupMapsAsync(CancellationToken cancellationToken) =>
        ((await wards.GetAllAsync(cancellationToken)).ToDictionary(w => w.Id), (await categories.GetAllAsync(cancellationToken)).ToDictionary(c => c.Id));

    private async Task<WardDto> WardDtoAsync(Ward ward, CancellationToken cancellationToken) =>
        ToDto(ward, await DepartmentNamesAsync([ward.DepartmentId], cancellationToken),
            (await beds.CountByWardAsync(cancellationToken)).GetValueOrDefault(ward.Id));

    private async Task<BedDto> BedDtoAsync(Bed bed, CancellationToken cancellationToken)
    {
        var (wardMap, categoryMap) = await SetupMapsAsync(cancellationToken);
        return ToDto(bed, wardMap, categoryMap);
    }

    private static BedCategoryDto ToDto(BedCategory c) => new(c.Id, c.Name, c.DailyRate, c.SuggestedDeposit, c.IsActive, c.RowVersion);

    private static WardDto ToDto(Ward w, IReadOnlyDictionary<Guid, string> departments, int bedCount) =>
        new(w.Id, w.Code, w.Name, w.Type, w.DepartmentId, w.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null, w.Building, w.Floor,
            w.IsActive, bedCount, w.RowVersion);

    private static BedDto ToDto(Bed b, IReadOnlyDictionary<Guid, Ward> wardMap, IReadOnlyDictionary<Guid, BedCategory> categoryMap)
    {
        var category = categoryMap.GetValueOrDefault(b.CategoryId);
        return new BedDto(b.Id, b.WardId, wardMap.GetValueOrDefault(b.WardId)?.Name, b.BedNumber, b.Room, b.CategoryId, category?.Name,
            category?.DailyRate ?? 0, b.Status, b.StatusNote, b.IsActive, b.RowVersion);
    }
}
