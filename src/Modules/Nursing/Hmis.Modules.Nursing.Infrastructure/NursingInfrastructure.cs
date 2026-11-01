using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Nursing.Application;
using Hmis.Modules.Nursing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Nursing.Infrastructure;

/// <summary>Owns the "nursing" schema.</summary>
public sealed class NursingDbContext(DbContextOptions<NursingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "nursing";

    public override string Schema => SchemaName;

    public override string ModuleName => "Nursing";

    public DbSet<NurseAssignment> NurseAssignments => Set<NurseAssignment>();
    public DbSet<MedicationAdministration> MedicationAdministrations => Set<MedicationAdministration>();
    public DbSet<IntakeOutputEntry> IntakeOutputEntries => Set<IntakeOutputEntry>();
    public DbSet<NursingNote> NursingNotes => Set<NursingNote>();
    public DbSet<ShiftHandover> ShiftHandovers => Set<ShiftHandover>();
}

public sealed class NursingUnitOfWork(NursingDbContext context) : UnitOfWork<NursingDbContext>(context), INursingUnitOfWork;

public sealed class NurseAssignmentConfiguration : IEntityTypeConfiguration<NurseAssignment>
{
    public void Configure(EntityTypeBuilder<NurseAssignment> builder)
    {
        builder.ToTable("NurseAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Shift).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(x => new { x.WardId, x.Date, x.Shift, x.EmployeeId }).IsUnique();
        // One nurse in charge per ward and shift.
        builder.HasIndex(x => new { x.WardId, x.Date, x.Shift }).IsUnique().HasFilter("\"IsInCharge\"")
            .HasDatabaseName("IX_NurseAssignments_InCharge");
        builder.HasIndex(x => new { x.EmployeeId, x.Date });
    }
}

public sealed class MedicationAdministrationConfiguration : IEntityTypeConfiguration<MedicationAdministration>
{
    public void Configure(EntityTypeBuilder<MedicationAdministration> builder)
    {
        builder.ToTable("MedicationAdministrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DoseGiven).HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(250);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.ErrorReason).HasMaxLength(250);
        // A scheduled dose is recorded once (entries marked in error do not count).
        builder.HasIndex(x => new { x.MedicationOrderId, x.ScheduledAt }).IsUnique()
            .HasFilter("\"ScheduledAt\" IS NOT NULL AND NOT \"IsEnteredInError\"");
        builder.HasIndex(x => new { x.AdmissionId, x.ScheduledAt });
        builder.HasIndex(x => new { x.AdmissionId, x.OccurredAt });
    }
}

public sealed class IntakeOutputEntryConfiguration : IEntityTypeConfiguration<IntakeOutputEntry>
{
    public void Configure(EntityTypeBuilder<IntakeOutputEntry> builder)
    {
        builder.ToTable("IntakeOutputEntries", t => t.HasCheckConstraint("CK_IntakeOutputEntries_Volume", "\"VolumeMl\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.ErrorReason).HasMaxLength(250);
        builder.HasIndex(x => new { x.AdmissionId, x.MeasuredAt });
    }
}

public sealed class NursingNoteConfiguration : IEntityTypeConfiguration<NursingNote>
{
    public void Configure(EntityTypeBuilder<NursingNote> builder)
    {
        builder.ToTable("NursingNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Text).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ErrorReason).HasMaxLength(250);
        builder.HasIndex(x => new { x.AdmissionId, x.CreatedAt });
    }
}

public sealed class ShiftHandoverConfiguration : IEntityTypeConfiguration<ShiftHandover>
{
    public void Configure(EntityTypeBuilder<ShiftHandover> builder)
    {
        builder.ToTable("ShiftHandovers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Shift).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Summary).HasMaxLength(4000);
        builder.HasIndex(x => new { x.WardId, x.Date, x.Shift }).IsUnique();
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.HandoverId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class HandoverItemConfiguration : IEntityTypeConfiguration<HandoverItem>
{
    public void Configure(EntityTypeBuilder<HandoverItem> builder)
    {
        builder.ToTable("HandoverItems");
        builder.HasKey(x => x.Id);
        // Items are added through the handover with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Note).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.HandoverId);
    }
}

public sealed class NurseAssignmentRepository(NursingDbContext context)
    : BaseRepository<NurseAssignment, NursingDbContext>(context), INurseAssignmentRepository
{
    public async Task<IReadOnlyList<NurseAssignment>> SearchAsync(DateOnly from, DateOnly to, Guid? wardId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(a => a.Date >= from && a.Date <= to && (wardId == null || a.WardId == wardId))
            .OrderBy(a => a.Date).ThenBy(a => a.Shift).ThenBy(a => a.WardId).ThenByDescending(a => a.IsInCharge)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NurseAssignment>> GetForShiftAsync(DateOnly date, Shift shift, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(a => a.Date == date && a.Shift == shift).ToListAsync(cancellationToken);
}

public sealed class MedicationAdministrationRepository(NursingDbContext context)
    : BaseRepository<MedicationAdministration, NursingDbContext>(context), IMedicationAdministrationRepository
{
    public async Task<IReadOnlyList<MedicationAdministration>> GetForAdmissionsAsync(
        IReadOnlyCollection<Guid> admissionIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(e => admissionIds.Contains(e.AdmissionId)
                        && ((e.ScheduledAt != null && e.ScheduledAt >= fromUtc && e.ScheduledAt < toUtc)
                            || (e.ScheduledAt == null && e.OccurredAt >= fromUtc && e.OccurredAt < toUtc)))
            .ToListAsync(cancellationToken);

    public Task<bool> IsRecordedAsync(Guid orderId, DateTime scheduledAt, CancellationToken cancellationToken) =>
        Set.AnyAsync(e => e.MedicationOrderId == orderId && e.ScheduledAt == scheduledAt && !e.IsEnteredInError, cancellationToken);
}

public sealed class IntakeOutputRepository(NursingDbContext context)
    : BaseRepository<IntakeOutputEntry, NursingDbContext>(context), IIntakeOutputRepository
{
    public async Task<IReadOnlyList<IntakeOutputEntry>> GetForAdmissionAsync(Guid admissionId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(e => e.AdmissionId == admissionId && e.MeasuredAt >= fromUtc && e.MeasuredAt < toUtc)
            .OrderBy(e => e.MeasuredAt)
            .ToListAsync(cancellationToken);
}

public sealed class NursingNoteRepository(NursingDbContext context)
    : BaseRepository<NursingNote, NursingDbContext>(context), INursingNoteRepository
{
    public async Task<IReadOnlyList<NursingNote>> GetForAdmissionAsync(Guid admissionId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(n => n.AdmissionId == admissionId).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
}

public sealed class ShiftHandoverRepository(NursingDbContext context)
    : BaseRepository<ShiftHandover, NursingDbContext>(context), IShiftHandoverRepository
{
    public Task<ShiftHandover?> GetWithItemsAsync(Guid id, CancellationToken cancellationToken) =>
        Set.Include(h => h.Items).FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public Task<ShiftHandover?> FindAsync(Guid wardId, DateOnly date, Shift shift, CancellationToken cancellationToken) =>
        Set.Include(h => h.Items).FirstOrDefaultAsync(h => h.WardId == wardId && h.Date == date && h.Shift == shift, cancellationToken);

    public async Task<IReadOnlyList<ShiftHandover>> GetForWardAsync(Guid wardId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Include(h => h.Items)
            .Where(h => h.WardId == wardId && h.Date >= from && h.Date <= to)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddNursingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NursingDbContext>(configuration, NursingDbContext.SchemaName);
        services.AddScoped<INursingUnitOfWork, NursingUnitOfWork>();
        services.AddScoped<INurseAssignmentRepository, NurseAssignmentRepository>();
        services.AddScoped<IMedicationAdministrationRepository, MedicationAdministrationRepository>();
        services.AddScoped<IIntakeOutputRepository, IntakeOutputRepository>();
        services.AddScoped<INursingNoteRepository, NursingNoteRepository>();
        services.AddScoped<IShiftHandoverRepository, ShiftHandoverRepository>();
        services.AddScoped<NursingSupport>();
        services.AddScoped<IWardNursingManager, WardNursingManager>();
        services.AddScoped<IMarManager, MarManager>();
        services.AddScoped<INursingRecordManager, NursingRecordManager>();
        services.AddScoped<IShiftManager, ShiftManager>();
        services.AddSingleton<IPermissionDefinitionProvider, NursingPermissionDefinitionProvider>();
        return services;
    }
}
