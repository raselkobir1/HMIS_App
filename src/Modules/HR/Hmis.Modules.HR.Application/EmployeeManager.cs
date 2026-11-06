using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.Modules.HR.Domain.Entities;

namespace Hmis.Modules.HR.Application;

public interface IHrUnitOfWork : IUnitOfWork;

public interface IEmployeeRepository : IRepository<Employee>, IConcurrencyRepository<Employee>
{
    Task<PagedResponse<Employee>> SearchAsync(EmployeeListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<Employee>> LookupCurrentAsync(string? search, int take, CancellationToken cancellationToken);

    Task<int> CountCurrentInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken);
}

public interface IEmployeeManager
{
    Task<PagedResponse<EmployeeListItemDto>> SearchAsync(EmployeeListQuery query, CancellationToken cancellationToken);

    Task<EmployeeDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmployeeLookupItemDto>> LookupAsync(EmployeeLookupQuery query, CancellationToken cancellationToken);

    Task<EmployeeDetailDto> CreateAsync(EmployeeCreateDto request, CancellationToken cancellationToken);

    Task<EmployeeDetailDto> UpdateAsync(Guid id, EmployeeUpdateDto request, CancellationToken cancellationToken);

    Task<EmployeeDetailDto> ChangeStatusAsync(Guid id, EmployeeStatusUpdateDto request, CancellationToken cancellationToken);
}

public sealed class EmployeeManager(
    IEmployeeRepository repository,
    IHrUnitOfWork unitOfWork,
    IDepartmentLookup departmentLookup,
    IDesignationLookup designationLookup,
    INumberSequenceService numberSequences,
    IUserAccountService userAccounts) : IEmployeeManager
{
    public async Task<PagedResponse<EmployeeListItemDto>> SearchAsync(EmployeeListQuery query, CancellationToken cancellationToken)
    {
        var page = await repository.SearchAsync(query, cancellationToken);
        var (departments, designations) = await LoadNamesAsync(page.Items, cancellationToken);

        return page.Map(e => new EmployeeListItemDto(
            e.Id, e.EmployeeCode, e.FullName, e.MobileNumber,
            e.DepartmentId, departments.GetValueOrDefault(e.DepartmentId),
            e.DesignationId, designations.GetValueOrDefault(e.DesignationId),
            e.EmploymentType, e.JoiningDate, e.Status));
    }

    public async Task<EmployeeDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDetailAsync(await GetAsync(id, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<EmployeeLookupItemDto>> LookupAsync(EmployeeLookupQuery query, CancellationToken cancellationToken)
    {
        var employees = await repository.LookupCurrentAsync(query.Search?.Trim(), query.Take, cancellationToken);
        var (_, designations) = await LoadNamesAsync(employees, cancellationToken);
        return employees
            .Select(e => new EmployeeLookupItemDto(e.Id, e.EmployeeCode, e.FullName, designations.GetValueOrDefault(e.DesignationId)))
            .ToList();
    }

    public async Task<EmployeeDetailDto> CreateAsync(EmployeeCreateDto request, CancellationToken cancellationToken)
    {
        await EnsureActiveDepartmentAsync(request.DepartmentId, cancellationToken);
        await EnsureActiveDesignationAsync(request.DesignationId, cancellationToken);

        // Allocated by Administration in its own atomic statement; a failed save below leaves a gap, never a duplicate.
        var code = await numberSequences.NextAsync(HrNumberSequences.EmployeeCode, cancellationToken);

        var employee = Employee.Create(code, request.FullName, request.Gender, request.DateOfBirth, request.MobileNumber,
            request.Email, request.Address, request.DepartmentId, request.DesignationId, request.EmploymentType, request.JoiningDate);

        await repository.AddAsync(employee, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(employee, cancellationToken);
    }

    public async Task<EmployeeDetailDto> UpdateAsync(Guid id, EmployeeUpdateDto request, CancellationToken cancellationToken)
    {
        var employee = await GetAsync(id, cancellationToken);
        repository.SetExpectedRowVersion(employee, request.RowVersion);

        // Moving to another department/designation requires an active one; keeping the current one is always allowed.
        if (request.DepartmentId != employee.DepartmentId)
        {
            await EnsureActiveDepartmentAsync(request.DepartmentId, cancellationToken);
        }

        if (request.DesignationId != employee.DesignationId)
        {
            await EnsureActiveDesignationAsync(request.DesignationId, cancellationToken);
        }

        employee.Update(request.FullName, request.Gender, request.DateOfBirth, request.MobileNumber, request.Email,
            request.Address, request.DepartmentId, request.DesignationId, request.EmploymentType, request.JoiningDate);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(employee, cancellationToken);
    }

    public async Task<EmployeeDetailDto> ChangeStatusAsync(Guid id, EmployeeStatusUpdateDto request, CancellationToken cancellationToken)
    {
        var employee = await GetAsync(id, cancellationToken);
        employee.ChangeStatus(request.Status, request.ExitDate);

        if (!employee.IsCurrent)
        {
            // Leaving staff lose their login at once. Identity saves first so one of its rules (e.g. the last active
            // Super Administrator) blocks the status change cleanly; if the HR save below still fails, the login is
            // already off and an administrator re-enables it.
            // Returning to Active/OnLeave does NOT reactivate the login: an administrator does that deliberately in
            // Security > Users after checking the person really is back.
            await userAccounts.DeactivateForEmployeeAsync(employee.Id,
                $"Employee {employee.EmployeeCode} marked {employee.Status} (exit date {employee.ExitDate:yyyy-MM-dd})", cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(employee, cancellationToken);
    }

    private async Task<Employee> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Employee), id);

    private async Task EnsureActiveDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        if (await departmentLookup.GetAsync(departmentId, cancellationToken) is not { IsActive: true })
        {
            throw new BusinessException(HrErrorCodes.DepartmentInvalid, "Select an active department.", "departmentId");
        }
    }

    private async Task EnsureActiveDesignationAsync(Guid designationId, CancellationToken cancellationToken)
    {
        if (await designationLookup.GetAsync(designationId, cancellationToken) is not { IsActive: true })
        {
            throw new BusinessException(HrErrorCodes.DesignationInvalid, "Select an active designation.", "designationId");
        }
    }

    /// <summary>Resolves department/designation names through Administration contracts (no cross-schema joins).</summary>
    private async Task<(Dictionary<Guid, string> Departments, Dictionary<Guid, string> Designations)> LoadNamesAsync(
        IEnumerable<Employee> employees, CancellationToken cancellationToken)
    {
        var list = employees.ToList();
        var departments = await departmentLookup.GetManyAsync(list.Select(e => e.DepartmentId).Distinct().ToList(), cancellationToken);
        var designations = await designationLookup.GetManyAsync(list.Select(e => e.DesignationId).Distinct().ToList(), cancellationToken);
        return (departments.ToDictionary(d => d.Id, d => d.Name), designations.ToDictionary(d => d.Id, d => d.Name));
    }

    private async Task<EmployeeDetailDto> ToDetailAsync(Employee e, CancellationToken cancellationToken)
    {
        var (departments, designations) = await LoadNamesAsync([e], cancellationToken);
        return new EmployeeDetailDto(
            e.Id, e.EmployeeCode, e.FullName, e.Gender, e.DateOfBirth, e.MobileNumber, e.Email, e.Address,
            e.DepartmentId, departments.GetValueOrDefault(e.DepartmentId),
            e.DesignationId, designations.GetValueOrDefault(e.DesignationId),
            e.EmploymentType, e.JoiningDate, e.Status, e.ExitDate, e.CreatedAt, e.RowVersion);
    }
}

/// <summary>Contracts HR provides to other modules.</summary>
public sealed class HrContractServices(IEmployeeRepository repository) : IEmployeeLookup, IDepartmentUsageGuard
{
    public async Task<EmployeeSummary?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken) is { } e
            ? new EmployeeSummary(e.Id, e.EmployeeCode, e.FullName, e.DepartmentId, e.DesignationId, e.Status.ToString(), e.IsCurrent)
            : null;

    public async Task<string?> GetDeactivationBlockerAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var count = await repository.CountCurrentInDepartmentAsync(departmentId, cancellationToken);
        return count == 0
            ? null
            : $"{count} current employee(s) belong to this department. Move them to another department first.";
    }
}
