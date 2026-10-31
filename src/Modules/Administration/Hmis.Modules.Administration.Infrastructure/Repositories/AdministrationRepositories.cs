using System.Data.Common;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Administration.Application.DTOs;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Domain.Entities;
using Hmis.Modules.Administration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hmis.Modules.Administration.Infrastructure.Repositories;

public sealed class HospitalProfileRepository(AdministrationDbContext context)
    : BaseRepository<HospitalProfile, AdministrationDbContext>(context), IHospitalProfileRepository
{
    public Task<HospitalProfile?> GetAsync(CancellationToken cancellationToken) =>
        Set.OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
}

public sealed class SystemSettingsRepository(AdministrationDbContext context)
    : BaseRepository<SystemSettings, AdministrationDbContext>(context), ISystemSettingsRepository
{
    public Task<SystemSettings?> GetAsync(CancellationToken cancellationToken) =>
        Set.OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(cancellationToken);
}

public sealed class DepartmentRepository(AdministrationDbContext context)
    : BaseRepository<Department, AdministrationDbContext>(context), IDepartmentRepository
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken) => Set.AnyAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(d => d.Code == code && d.Id != excludeId, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(d => d.NormalizedName == normalizedName && d.Id != excludeId, cancellationToken);

    public Task<bool> HasActiveChildrenAsync(Guid id, CancellationToken cancellationToken) =>
        Set.AnyAsync(d => d.ParentDepartmentId == id && d.IsActive, cancellationToken);

    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken) =>
        Set.AnyAsync(d => d.ParentDepartmentId == id, cancellationToken);

    public async Task<PagedResponse<DepartmentDto>> SearchAsync(DepartmentListQuery query, CancellationToken cancellationToken)
    {
        var departments = Set.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            departments = departments.Where(d => EF.Functions.ILike(d.Name, pattern) || EF.Functions.ILike(d.Code, pattern));
        }

        if (query.IsActive.HasValue)
        {
            departments = departments.Where(d => d.IsActive == query.IsActive.Value);
        }

        if (query.Type.HasValue)
        {
            departments = departments.Where(d => d.Type == query.Type.Value);
        }

        if (query.ParentDepartmentId.HasValue)
        {
            departments = departments.Where(d => d.ParentDepartmentId == query.ParentDepartmentId.Value);
        }

        var descending = query.SortDirection == SortDirection.Desc;
        departments = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => descending ? departments.OrderByDescending(d => d.Code) : departments.OrderBy(d => d.Code),
            "type" => descending ? departments.OrderByDescending(d => d.Type).ThenBy(d => d.Name) : departments.OrderBy(d => d.Type).ThenBy(d => d.Name),
            _ => descending ? departments.OrderByDescending(d => d.Name) : departments.OrderBy(d => d.Name)
        };

        var total = await departments.LongCountAsync(cancellationToken);
        var items = await Project(departments.Skip(query.Skip).Take(query.PageSize)).ToListAsync(cancellationToken);
        return PagedResponse<DepartmentDto>.Create(items, total, query);
    }

    public Task<DepartmentDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken) =>
        Project(Set.AsNoTracking().Where(d => d.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DepartmentLookupItemDto>> GetActiveLookupAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentLookupItemDto(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.Type))
            .ToListAsync(cancellationToken);

    private IQueryable<DepartmentDto> Project(IQueryable<Department> departments) =>
        departments.Select(d => new DepartmentDto(
            d.Id,
            d.Code,
            d.Name,
            d.Type,
            d.ParentDepartmentId,
            Set.Where(p => p.Id == d.ParentDepartmentId).Select(p => p.Name).FirstOrDefault(),
            d.Description,
            d.IsActive,
            Set.Count(c => c.ParentDepartmentId == d.Id),
            d.RowVersion));
}

public sealed class DesignationRepository(AdministrationDbContext context)
    : BaseRepository<Designation, AdministrationDbContext>(context), IDesignationRepository
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken) => Set.AnyAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(d => d.NormalizedName == normalizedName && d.Id != excludeId, cancellationToken);

    public async Task<PagedResponse<DesignationDto>> SearchAsync(DesignationListQuery query, CancellationToken cancellationToken)
    {
        var designations = Set.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            designations = designations.Where(d => EF.Functions.ILike(d.Name, pattern));
        }

        if (query.IsActive.HasValue)
        {
            designations = designations.Where(d => d.IsActive == query.IsActive.Value);
        }

        if (query.Category.HasValue)
        {
            designations = designations.Where(d => d.Category == query.Category.Value);
        }

        var descending = query.SortDirection == SortDirection.Desc;
        designations = query.SortBy?.ToLowerInvariant() == "category"
            ? (descending ? designations.OrderByDescending(d => d.Category) : designations.OrderBy(d => d.Category)).ThenBy(d => d.Name)
            : descending ? designations.OrderByDescending(d => d.Name) : designations.OrderBy(d => d.Name);

        var total = await designations.LongCountAsync(cancellationToken);
        var items = await designations
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(d => new DesignationDto(d.Id, d.Name, d.Category, d.IsActive, d.RowVersion))
            .ToListAsync(cancellationToken);
        return PagedResponse<DesignationDto>.Create(items, total, query);
    }

    public async Task<IReadOnlyList<DesignationLookupItemDto>> GetActiveLookupAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DesignationLookupItemDto(d.Id, d.Name, d.Category))
            .ToListAsync(cancellationToken);
}

public sealed class NumberSequenceRepository(AdministrationDbContext context)
    : BaseRepository<NumberSequence, AdministrationDbContext>(context), INumberSequenceRepository
{
    // One statement: PostgreSQL row-locks the sequence row for the UPDATE, so concurrent callers are serialized
    // and every caller receives a distinct value. The yearly reset happens inside the same statement.
    private const string AllocateSql = """
        UPDATE admin."NumberSequences"
        SET "CurrentValue" = CASE WHEN "ResetPolicy" = 'Yearly' AND "PeriodYear" <> @year THEN 1 ELSE "CurrentValue" + 1 END,
            "PeriodYear" = @year
        WHERE "Key" = @key
        RETURNING "CurrentValue", "Prefix", "YearFormat", "PadLength"
        """;

    public Task<NumberSequence?> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

    public async Task<IReadOnlyList<NumberSequence>> GetAllAsync(CancellationToken cancellationToken) =>
        await Set.OrderBy(s => s.Key).ToListAsync(cancellationToken);

    public async Task<AllocatedNumber?> AllocateNextAsync(string key, int currentYear, CancellationToken cancellationToken)
    {
        await Context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = Context.Database.GetDbConnection().CreateCommand();
            command.Transaction = Context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = AllocateSql;
            AddParameter(command, "year", currentYear);
            AddParameter(command, "key", key);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new AllocatedNumber(
                reader.GetInt64(0),
                reader.GetString(1),
                Enum.Parse<YearFormat>(reader.GetString(2)),
                reader.GetInt32(3));
        }
        finally
        {
            await Context.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
