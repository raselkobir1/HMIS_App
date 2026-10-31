using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Domain.Entities;

namespace Hmis.Modules.Administration.Application.Managers;

public sealed class DepartmentManager(IDepartmentRepository repository, IAdministrationUnitOfWork unitOfWork) : IDepartmentManager
{
    public Task<PagedResponse<DepartmentDto>> SearchAsync(DepartmentListQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public async Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetDtoAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Department), id);

    public Task<IReadOnlyList<DepartmentLookupItemDto>> GetLookupAsync(CancellationToken cancellationToken) =>
        repository.GetActiveLookupAsync(cancellationToken);

    public async Task<DepartmentDto> CreateAsync(DepartmentCreateDto request, CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(request.Code, request.Name, null, cancellationToken);
        var parent = await GetParentAsync(request.ParentDepartmentId, cancellationToken);

        var department = Department.Create(request.Code, request.Name, request.Type, parent, request.Description);
        await repository.AddAsync(department, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(department.Id, cancellationToken);
    }

    public async Task<DepartmentDto> UpdateAsync(Guid id, DepartmentUpdateDto request, CancellationToken cancellationToken)
    {
        var department = await GetDepartmentAsync(id, cancellationToken);
        repository.SetExpectedRowVersion(department, request.RowVersion);
        await EnsureUniqueAsync(request.Code, request.Name, id, cancellationToken);
        var parent = await GetParentAsync(request.ParentDepartmentId, cancellationToken);

        // Keep the hierarchy at two levels: a department with sub-departments cannot become a sub-department.
        if (parent is not null && await repository.HasChildrenAsync(id, cancellationToken))
        {
            throw new BusinessException(AdministrationErrorCodes.DepartmentHasChildren,
                "This department has sub-departments, so it cannot be placed under another department.", "parentDepartmentId");
        }

        department.Update(request.Code, request.Name, request.Type, parent, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DepartmentDto> SetStatusAsync(Guid id, StatusUpdateDto request, CancellationToken cancellationToken)
    {
        var department = await GetDepartmentAsync(id, cancellationToken);

        if (request.IsActive)
        {
            if (department.ParentDepartmentId is { } parentId &&
                await repository.GetByIdAsync(parentId, cancellationToken) is { IsActive: false })
            {
                throw new BusinessException(AdministrationErrorCodes.DepartmentParentInactive,
                    "Activate the parent department first.");
            }

            department.Activate();
        }
        else
        {
            if (await repository.HasActiveChildrenAsync(id, cancellationToken))
            {
                throw new BusinessException(AdministrationErrorCodes.DepartmentHasActiveChildren,
                    "Deactivate its active sub-departments first.");
            }

            // When the HR module exists, this must also block departments with active employees (via IEmployeeLookup).
            department.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<Department> GetDepartmentAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Department), id);

    private async Task<Department?> GetParentAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        return await repository.GetByIdAsync(parentId.Value, cancellationToken)
            ?? throw new BusinessException(AdministrationErrorCodes.DepartmentNotFound, "The parent department does not exist.", "parentDepartmentId");
    }

    private async Task EnsureUniqueAsync(string code, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await repository.CodeExistsAsync(Department.NormalizeCode(code), excludeId, cancellationToken))
        {
            throw new ConflictException($"Department code '{code.Trim().ToUpperInvariant()}' is already in use.", AdministrationErrorCodes.DepartmentCodeTaken);
        }

        if (await repository.NameExistsAsync(name.Trim().ToUpperInvariant(), excludeId, cancellationToken))
        {
            throw new ConflictException($"A department named '{name.Trim()}' already exists.", AdministrationErrorCodes.DepartmentNameTaken);
        }
    }
}

public sealed class DesignationManager(IDesignationRepository repository, IAdministrationUnitOfWork unitOfWork) : IDesignationManager
{
    public Task<PagedResponse<DesignationDto>> SearchAsync(DesignationListQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public Task<IReadOnlyList<DesignationLookupItemDto>> GetLookupAsync(CancellationToken cancellationToken) =>
        repository.GetActiveLookupAsync(cancellationToken);

    public async Task<DesignationDto> CreateAsync(DesignationCreateDto request, CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(request.Name, null, cancellationToken);
        var designation = Designation.Create(request.Name, request.Category);
        await repository.AddAsync(designation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(designation);
    }

    public async Task<DesignationDto> UpdateAsync(Guid id, DesignationUpdateDto request, CancellationToken cancellationToken)
    {
        var designation = await GetAsync(id, cancellationToken);
        repository.SetExpectedRowVersion(designation, request.RowVersion);
        await EnsureUniqueAsync(request.Name, id, cancellationToken);
        designation.Update(request.Name, request.Category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(designation);
    }

    public async Task<DesignationDto> SetStatusAsync(Guid id, StatusUpdateDto request, CancellationToken cancellationToken)
    {
        var designation = await GetAsync(id, cancellationToken);
        if (request.IsActive)
        {
            designation.Activate();
        }
        else
        {
            designation.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(designation);
    }

    private async Task<Designation> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Designation), id);

    private async Task EnsureUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await repository.NameExistsAsync(name.Trim().ToUpperInvariant(), excludeId, cancellationToken))
        {
            throw new ConflictException($"A designation named '{name.Trim()}' already exists.", AdministrationErrorCodes.DesignationNameTaken);
        }
    }

    private static DesignationDto ToDto(Designation d) => new(d.Id, d.Name, d.Category, d.IsActive, d.RowVersion);
}
