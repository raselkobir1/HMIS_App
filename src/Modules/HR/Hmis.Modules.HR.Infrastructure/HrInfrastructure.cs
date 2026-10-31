using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.HR;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.HR.Application;
using Hmis.Modules.HR.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.HR.Infrastructure;

/// <summary>Owns the "hr" schema.</summary>
public sealed class HrDbContext(DbContextOptions<HrDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "hr";

    public override string Schema => SchemaName;

    public override string ModuleName => "HR";

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();
}

public sealed class HrUnitOfWork(HrDbContext context) : UnitOfWork<HrDbContext>(context), IHrUnitOfWork;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.IsCurrent);

        builder.Property(x => x.EmployeeCode).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.EmployeeCode).IsUnique();
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.MobileNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        // DepartmentId / DesignationId reference admin tables by id only (module ownership; validated via contracts).
        builder.HasIndex(x => new { x.DepartmentId, x.Status });
        builder.HasIndex(x => x.DesignationId);
        builder.HasIndex(x => x.MobileNumber);
        builder.HasIndex(x => x.FullName);
    }
}

public sealed class EmployeeRepository(HrDbContext context)
    : BaseRepository<Employee, HrDbContext>(context), IEmployeeRepository
{
    private static readonly EmploymentStatus[] CurrentStatuses = [EmploymentStatus.Active, EmploymentStatus.OnLeave];

    public async Task<PagedResponse<Employee>> SearchAsync(EmployeeListQuery query, CancellationToken cancellationToken)
    {
        var employees = Set.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            employees = employees.Where(e =>
                EF.Functions.ILike(e.FullName, pattern) ||
                EF.Functions.ILike(e.EmployeeCode, pattern) ||
                EF.Functions.ILike(e.MobileNumber, pattern));
        }

        if (query.DepartmentId.HasValue)
        {
            employees = employees.Where(e => e.DepartmentId == query.DepartmentId.Value);
        }

        if (query.DesignationId.HasValue)
        {
            employees = employees.Where(e => e.DesignationId == query.DesignationId.Value);
        }

        if (query.Status.HasValue)
        {
            employees = employees.Where(e => e.Status == query.Status.Value);
        }

        if (query.EmploymentType.HasValue)
        {
            employees = employees.Where(e => e.EmploymentType == query.EmploymentType.Value);
        }

        var descending = query.SortDirection == SortDirection.Desc;
        employees = query.SortBy?.ToLowerInvariant() switch
        {
            "employeecode" => descending ? employees.OrderByDescending(e => e.EmployeeCode) : employees.OrderBy(e => e.EmployeeCode),
            "joiningdate" => descending ? employees.OrderByDescending(e => e.JoiningDate) : employees.OrderBy(e => e.JoiningDate),
            _ => descending ? employees.OrderByDescending(e => e.FullName) : employees.OrderBy(e => e.FullName)
        };

        var total = await employees.LongCountAsync(cancellationToken);
        var items = await employees.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<Employee>.Create(items, total, query);
    }

    public async Task<IReadOnlyList<Employee>> LookupCurrentAsync(string? search, int take, CancellationToken cancellationToken)
    {
        var employees = Set.AsNoTracking().Where(e => CurrentStatuses.Contains(e.Status));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            employees = employees.Where(e => EF.Functions.ILike(e.FullName, pattern) || EF.Functions.ILike(e.EmployeeCode, pattern));
        }

        return await employees.OrderBy(e => e.FullName).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountCurrentInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) =>
        Set.CountAsync(e => e.DepartmentId == departmentId && CurrentStatuses.Contains(e.Status), cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddHrModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<HrDbContext>(configuration, HrDbContext.SchemaName);
        services.AddScoped<IHrUnitOfWork, HrUnitOfWork>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeManager, EmployeeManager>();
        services.AddScoped<IDoctorProfileRepository, DoctorProfileRepository>();
        services.AddScoped<DoctorManager>();
        services.AddScoped<IDoctorManager>(sp => sp.GetRequiredService<DoctorManager>());
        services.AddScoped<IDoctorLookup>(sp => sp.GetRequiredService<DoctorManager>());

        services.AddScoped<HrContractServices>();
        services.AddScoped<IEmployeeLookup>(sp => sp.GetRequiredService<HrContractServices>());
        services.AddScoped<IDepartmentUsageGuard>(sp => sp.GetRequiredService<HrContractServices>());

        services.AddSingleton<IPermissionDefinitionProvider, HrPermissionDefinitionProvider>();
        services.AddSingleton<IPermissionDefinitionProvider, DoctorPermissionDefinitionProvider>();
        services.AddSingleton<INumberSequenceDefinitionProvider, HrNumberSequences>();
        return services;
    }
}
