using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Administration;
using Hmis.BuildingBlocks.Contracts.Ipd;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Ipd.Application;
using Hmis.Modules.Ipd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hmis.Modules.Ipd.Infrastructure;

/// <summary>Owns the "ipd" schema.</summary>
public sealed class IpdDbContext(DbContextOptions<IpdDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "ipd";

    public override string Schema => SchemaName;

    public override string ModuleName => "IPD";

    public DbSet<BedCategory> BedCategories => Set<BedCategory>();
    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<Admission> Admissions => Set<Admission>();
}

public sealed class IpdUnitOfWork(IpdDbContext context) : UnitOfWork<IpdDbContext>(context), IIpdUnitOfWork;

public sealed class BedCategoryConfiguration : IEntityTypeConfiguration<BedCategory>
{
    public void Configure(EntityTypeBuilder<BedCategory> builder)
    {
        builder.ToTable("BedCategories", t => t.HasCheckConstraint("CK_BedCategories_Amounts", "\"DailyRate\" >= 0 AND \"SuggestedDeposit\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DailyRate).HasPrecision(18, 2);
        builder.Property(x => x.SuggestedDeposit).HasPrecision(18, 2);
    }
}

public sealed class WardConfiguration : IEntityTypeConfiguration<Ward>
{
    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.ToTable("Wards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Building).HasMaxLength(100);
        builder.Property(x => x.Floor).HasMaxLength(30);
    }
}

public sealed class BedConfiguration : IEntityTypeConfiguration<Bed>
{
    public void Configure(EntityTypeBuilder<Bed> builder)
    {
        builder.ToTable("Beds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BedNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Room).HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.StatusNote).HasMaxLength(200);
        builder.Ignore(x => x.CanBeAssigned);
        builder.Ignore(x => x.UnavailableReason);
        builder.HasIndex(x => new { x.WardId, x.BedNumber }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasOne<Ward>().WithMany().HasForeignKey(x => x.WardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BedCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AdmissionConfiguration : IEntityTypeConfiguration<Admission>
{
    public void Configure(EntityTypeBuilder<Admission> builder)
    {
        builder.ToTable("Admissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AdmissionNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.AdmissionNumber).IsUnique();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SourceReference).HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.EmergencyContactName).HasMaxLength(100);
        builder.Property(x => x.EmergencyContactRelation).HasMaxLength(50);
        builder.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DischargeType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.FinalDiagnosis).HasMaxLength(500);
        builder.Property(x => x.HospitalCourse).HasMaxLength(4000);
        builder.Property(x => x.DischargeMedications).HasMaxLength(2000);
        builder.Property(x => x.DischargeAdvice).HasMaxLength(2000);
        builder.Property(x => x.ReleasedWithDue).HasPrecision(18, 2);
        builder.Property(x => x.ReleaseOverrideReason).HasMaxLength(250);
        builder.Property(x => x.ReopenReason).HasMaxLength(250);
        builder.Ignore(x => x.CurrentBed);
        builder.Ignore(x => x.IsActive);

        // One stay at a time per patient; the manager checks first, this backs it up against races.
        builder.HasIndex(x => x.PatientId).IsUnique().HasFilter("\"Status\" <> 'Discharged'").HasDatabaseName("IX_Admissions_PatientId_Active");
        builder.HasIndex(x => new { x.PatientId, x.AdmittedAt });
        builder.HasIndex(x => new { x.Status, x.AdmittedAt });
        builder.HasIndex(x => x.AttendingDoctorId);

        builder.HasMany(x => x.Beds).WithOne().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Beds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class BedAssignmentConfiguration : IEntityTypeConfiguration<BedAssignment>
{
    public void Configure(EntityTypeBuilder<BedAssignment> builder)
    {
        builder.ToTable("BedAssignments");
        builder.HasKey(x => x.Id);
        // Added through the Admission collection with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        // No double booking: a bed has at most one open stay.
        builder.HasIndex(x => x.BedId).IsUnique().HasFilter("\"EndedAt\" IS NULL").HasDatabaseName("IX_BedAssignments_BedId_Open");
        builder.HasIndex(x => x.AdmissionId);
        builder.HasOne<Bed>().WithMany().HasForeignKey(x => x.BedId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BedCategoryRepository(IpdDbContext context)
    : BaseRepository<BedCategory, IpdDbContext>(context), IBedCategoryRepository
{
    public async Task<IReadOnlyList<BedCategory>> GetAllAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BedCategory>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
}

public sealed class WardRepository(IpdDbContext context) : BaseRepository<Ward, IpdDbContext>(context), IWardRepository
{
    public async Task<IReadOnlyList<Ward>> GetAllAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking().OrderBy(w => w.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Ward>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(w => ids.Contains(w.Id)).ToListAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) => Set.AnyAsync(w => w.Code == code, cancellationToken);
}

public sealed class BedRepository(IpdDbContext context) : BaseRepository<Bed, IpdDbContext>(context), IBedRepository
{
    public async Task<IReadOnlyList<Bed>> SearchAsync(BedQuery query, CancellationToken cancellationToken)
    {
        var list = Set.AsNoTracking();
        if (query.WardId.HasValue)
        {
            list = list.Where(b => b.WardId == query.WardId.Value);
        }

        if (query.AssignableOnly == true)
        {
            list = list.Where(b => b.IsActive && b.Status == BedStatus.Available &&
                                   Context.Wards.Any(w => w.Id == b.WardId && w.IsActive));
        }

        return await list.OrderBy(b => b.WardId).ThenBy(b => b.BedNumber).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Bed>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(b => ids.Contains(b.Id)).ToListAsync(cancellationToken);

    public Task<bool> NumberExistsAsync(Guid wardId, string bedNumber, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(b => b.WardId == wardId && b.BedNumber == bedNumber && b.Id != excludeId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountByWardAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking().GroupBy(b => b.WardId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
}

public sealed class AdmissionRepository(IpdDbContext context)
    : BaseRepository<Admission, IpdDbContext>(context), IAdmissionRepository
{
    private static readonly AdmissionStatus[] InBed = [AdmissionStatus.Admitted, AdmissionStatus.DischargeOrdered];

    public Task<Admission?> GetWithBedsAsync(Guid id, CancellationToken cancellationToken) =>
        Set.Include(a => a.Beds).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<PagedResponse<Admission>> SearchAsync(AdmissionListQuery query, CancellationToken cancellationToken)
    {
        var list = Set.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            list = list.Where(a => EF.Functions.ILike(a.AdmissionNumber, pattern));
        }

        if (query.Status.HasValue)
        {
            list = list.Where(a => a.Status == query.Status.Value);
        }
        else if (query.CurrentOnly != false)
        {
            list = list.Where(a => a.Status != AdmissionStatus.Discharged);
        }

        if (query.WardId.HasValue)
        {
            var wardBeds = Context.Beds.Where(b => b.WardId == query.WardId.Value).Select(b => b.Id);
            list = list.Where(a => a.Beds.Any(s => s.EndedAt == null && wardBeds.Contains(s.BedId)));
        }

        if (query.DoctorId.HasValue)
        {
            list = list.Where(a => a.AttendingDoctorId == query.DoctorId.Value);
        }

        if (query.PatientIds is { Count: > 0 } patientIds)
        {
            list = list.Where(a => patientIds.Contains(a.PatientId));
        }
        else if (query.PatientId.HasValue)
        {
            list = list.Where(a => a.PatientId == query.PatientId.Value);
        }

        var total = await list.LongCountAsync(cancellationToken);
        var items = await list.Include(a => a.Beds).OrderByDescending(a => a.AdmittedAt)
            .Skip(query.Skip).Take(query.PageSize).AsSplitQuery().ToListAsync(cancellationToken);
        return PagedResponse<Admission>.Create(items, total, query);
    }

    public Task<bool> HasActiveAsync(Guid patientId, CancellationToken cancellationToken) =>
        Set.AnyAsync(a => a.PatientId == patientId && a.Status != AdmissionStatus.Discharged, cancellationToken);

    public async Task<IReadOnlyList<Admission>> GetInBedAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Include(a => a.Beds).Where(a => InBed.Contains(a.Status)).AsSplitQuery().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Admission>> GetActiveAsync(CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Include(a => a.Beds).Where(a => a.Status != AdmissionStatus.Discharged).AsSplitQuery().ToListAsync(cancellationToken);

    public Task<bool> IsAttendingDoctorAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken) =>
        Set.AnyAsync(a => a.AttendingDoctorId == doctorId && a.PatientId == patientId, cancellationToken);

    public async Task<IReadOnlyList<Admission>> GetActiveForBedsAsync(IReadOnlyCollection<Guid> bedIds, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Include(a => a.Beds)
            .Where(a => a.Status != AdmissionStatus.Discharged && a.Beds.Any(b => b.EndedAt == null && bedIds.Contains(b.BedId)))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
}

public sealed class IpdOptions
{
    public const string SectionName = "Ipd";

    /// <summary>How often bed rent is posted for patients in hospital. Each day is posted once (idempotent).</summary>
    public int BedChargeIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// Posts bed rent for every patient in a bed, shortly after startup and then periodically. Each stay is posted in its own
/// DI scope (own DbContexts): a failure for one stay cannot leave a poisoned context for the rest of the run.
/// </summary>
public sealed class BedChargeJob(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<BedChargeJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = Math.Max(1, configuration.GetSection(IpdOptions.SectionName).Get<IpdOptions>()?.BedChargeIntervalMinutes ?? 30);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Admission> inBed;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            inBed = await scope.ServiceProvider.GetRequiredService<IAdmissionRepository>().GetInBedAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Bed charge posting failed: patients in bed could not be loaded");
            return;
        }

        foreach (var admission in inBed)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<BedChargePoster>().PostAsync(admission, null, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Bed charges for admission {AdmissionNumber} could not be posted", admission.AdmissionNumber);
            }
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddIpdModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IpdDbContext>(configuration, IpdDbContext.SchemaName);
        services.AddScoped<IIpdUnitOfWork, IpdUnitOfWork>();
        services.AddScoped<IBedCategoryRepository, BedCategoryRepository>();
        services.AddScoped<IWardRepository, WardRepository>();
        services.AddScoped<IBedRepository, BedRepository>();
        services.AddScoped<IAdmissionRepository, AdmissionRepository>();
        services.AddScoped<BedChargePoster>();
        services.AddScoped<IBedSetupManager, BedSetupManager>();
        services.AddScoped<IAdmissionManager, AdmissionManager>();
        services.AddScoped<IInpatientLookup, InpatientLookup>();
        services.AddScoped<Hmis.BuildingBlocks.Contracts.Patient.IPatientMergeGuard, AdmissionMergeGuard>();
        services.AddHostedService<BedChargeJob>();
        services.AddSingleton<IPermissionDefinitionProvider, IpdPermissionDefinitionProvider>();
        services.AddSingleton<INumberSequenceDefinitionProvider, IpdNumberSequences>();
        return services;
    }
}
