using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Clinical;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Clinical.Application;
using Hmis.Modules.Clinical.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Clinical.Infrastructure;

/// <summary>Owns the "clinical" schema.</summary>
public sealed class ClinicalDbContext(DbContextOptions<ClinicalDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "clinical";

    public override string Schema => SchemaName;

    public override string ModuleName => "Clinical";

    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<ProgressNote> ProgressNotes => Set<ProgressNote>();
    public DbSet<MedicationOrder> MedicationOrders => Set<MedicationOrder>();
}

public sealed class ClinicalUnitOfWork(ClinicalDbContext context)
    : UnitOfWork<ClinicalDbContext>(context), IClinicalUnitOfWork;

public sealed class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.ToTable("Consultations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EncounterType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.EncounterNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ChiefComplaint).HasMaxLength(1000);
        builder.Property(x => x.HistoryOfPresentIllness).HasMaxLength(4000);
        builder.Property(x => x.Examination).HasMaxLength(4000);
        builder.Property(x => x.Advice).HasMaxLength(2000);
        builder.Property(x => x.FollowUpInstructions).HasMaxLength(500);
        builder.Property(x => x.ReferralTo).HasMaxLength(200);
        builder.Property(x => x.ReferralReason).HasMaxLength(500);

        // One consultation per encounter.
        builder.HasIndex(x => new { x.EncounterType, x.EncounterId }).IsUnique();
        builder.HasIndex(x => new { x.PatientId, x.EncounterDate });
        builder.HasIndex(x => new { x.DoctorId, x.EncounterDate });

        builder.HasMany(x => x.Diagnoses).WithOne().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Prescription).WithOne().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Investigations).WithOne().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Addenda).WithOne().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Diagnoses).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Prescription).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Investigations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Addenda).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ConsultationDiagnosisConfiguration : IEntityTypeConfiguration<ConsultationDiagnosis>
{
    public void Configure(EntityTypeBuilder<ConsultationDiagnosis> builder)
    {
        builder.ToTable("ConsultationDiagnoses");
        builder.HasKey(x => x.Id);
        // Lines are added through the Consultation collection with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        // "One primary diagnosis" is enforced by the domain, not a unique index: swapping primary and secondary in one
        // save would briefly violate a row-by-row unique check.
        builder.HasIndex(x => x.ConsultationId);
    }
}

public sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems", t => t.HasCheckConstraint("CK_PrescriptionItems_Quantity", "\"Quantity\" IS NULL OR \"Quantity\" > 0"));
        builder.HasKey(x => x.Id);
        // Lines are added through the Consultation collection with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.MedicineName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Form).HasMaxLength(30);
        builder.Property(x => x.Strength).HasMaxLength(50);
        builder.Property(x => x.Route).HasMaxLength(30);
        builder.Property(x => x.DosePattern).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Timing).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DurationUnit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Instructions).HasMaxLength(300);
        builder.HasIndex(x => x.ConsultationId);
    }
}

public sealed class InvestigationRequestConfiguration : IEntityTypeConfiguration<InvestigationRequest>
{
    public void Configure(EntityTypeBuilder<InvestigationRequest> builder)
    {
        builder.ToTable("InvestigationRequests");
        builder.HasKey(x => x.Id);
        // Lines are added through the Consultation collection with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.HasIndex(x => x.ConsultationId);
    }
}

public sealed class ConsultationAddendumConfiguration : IEntityTypeConfiguration<ConsultationAddendum>
{
    public void Configure(EntityTypeBuilder<ConsultationAddendum> builder)
    {
        builder.ToTable("ConsultationAddenda");
        builder.HasKey(x => x.Id);
        // Lines are added through the Consultation collection with ids generated in the domain: insert, never assume they exist.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.ConsultationId);
    }
}

public sealed class VitalSignConfiguration : IEntityTypeConfiguration<VitalSign>
{
    public void Configure(EntityTypeBuilder<VitalSign> builder)
    {
        builder.ToTable("VitalSigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EncounterType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TemperatureF).HasPrecision(4, 1);
        builder.Property(x => x.WeightKg).HasPrecision(5, 2);
        builder.Property(x => x.HeightCm).HasPrecision(5, 1);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.ErrorReason).HasMaxLength(250);
        builder.Ignore(x => x.Bmi);
        builder.HasIndex(x => new { x.EncounterType, x.EncounterId });
        builder.HasIndex(x => x.PatientId);
    }
}

public sealed class ProgressNoteConfiguration : IEntityTypeConfiguration<ProgressNote>
{
    public void Configure(EntityTypeBuilder<ProgressNote> builder)
    {
        builder.ToTable("ProgressNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Subjective).HasMaxLength(4000);
        builder.Property(x => x.Objective).HasMaxLength(4000);
        builder.Property(x => x.Assessment).HasMaxLength(4000);
        builder.Property(x => x.Plan).HasMaxLength(4000);
        builder.Property(x => x.ErrorReason).HasMaxLength(250);
        builder.HasIndex(x => new { x.AdmissionId, x.CreatedAt });
        builder.HasIndex(x => x.PatientId);
    }
}

public sealed class MedicationOrderConfiguration : IEntityTypeConfiguration<MedicationOrder>
{
    public void Configure(EntityTypeBuilder<MedicationOrder> builder)
    {
        builder.ToTable("MedicationOrders", t => t.HasCheckConstraint("CK_MedicationOrders_Duration", "\"DurationDays\" IS NULL OR \"DurationDays\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MedicineName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Strength).HasMaxLength(50);
        builder.Property(x => x.Form).HasMaxLength(30);
        builder.Property(x => x.Route).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Dose).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Frequency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.PrnIndication).HasMaxLength(200);
        builder.Property(x => x.Instructions).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.StopReason).HasMaxLength(250);
        builder.Ignore(x => x.PlannedEndsAt);
        builder.Ignore(x => x.EndsAt);
        builder.HasIndex(x => x.AdmissionId);
        builder.HasIndex(x => x.PatientId);
    }
}

public sealed class ProgressNoteRepository(ClinicalDbContext context)
    : BaseRepository<ProgressNote, ClinicalDbContext>(context), IProgressNoteRepository
{
    public async Task<IReadOnlyList<ProgressNote>> GetForAdmissionAsync(Guid admissionId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(n => n.AdmissionId == admissionId).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
}

public sealed class MedicationOrderRepository(ClinicalDbContext context)
    : BaseRepository<MedicationOrder, ClinicalDbContext>(context), IMedicationOrderRepository
{
    public async Task<IReadOnlyList<MedicationOrder>> GetForAdmissionsAsync(IReadOnlyCollection<Guid> admissionIds, CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(o => admissionIds.Contains(o.AdmissionId)).OrderBy(o => o.CreatedAt).ToListAsync(cancellationToken);
}

public sealed class ConsultationRepository(ClinicalDbContext context)
    : BaseRepository<Consultation, ClinicalDbContext>(context), IConsultationRepository
{
    public Task<Consultation?> GetWithLinesAsync(Guid id, CancellationToken cancellationToken) =>
        WithLines(Set).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Consultation?> GetByEncounterAsync(EncounterType encounterType, Guid encounterId, CancellationToken cancellationToken) =>
        WithLines(Set).FirstOrDefaultAsync(c => c.EncounterType == encounterType && c.EncounterId == encounterId, cancellationToken);

    public async Task<IReadOnlyList<Consultation>> GetForPatientAsync(IReadOnlyCollection<Guid> patientIds, int take, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Include(c => c.Diagnoses)
            .Include(c => c.Prescription)
            .AsSplitQuery()
            .Where(c => patientIds.Contains(c.PatientId))
            .OrderByDescending(c => c.EncounterDate).ThenByDescending(c => c.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    private static IQueryable<Consultation> WithLines(IQueryable<Consultation> query) =>
        query.Include(c => c.Diagnoses)
            .Include(c => c.Prescription)
            .Include(c => c.Investigations)
            .Include(c => c.Addenda)
            .AsSplitQuery();
}

public sealed class VitalSignRepository(ClinicalDbContext context)
    : BaseRepository<VitalSign, ClinicalDbContext>(context), IVitalSignRepository
{
    public async Task<IReadOnlyList<VitalSign>> GetForEncounterAsync(EncounterType encounterType, Guid encounterId, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(v => v.EncounterType == encounterType && v.EncounterId == encounterId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddClinicalModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ClinicalDbContext>(configuration, ClinicalDbContext.SchemaName);
        services.AddScoped<IClinicalUnitOfWork, ClinicalUnitOfWork>();
        services.AddScoped<IConsultationRepository, ConsultationRepository>();
        services.AddScoped<IVitalSignRepository, VitalSignRepository>();
        services.AddScoped<IClinicalManager, ClinicalManager>();
        services.AddScoped<IProgressNoteRepository, ProgressNoteRepository>();
        services.AddScoped<IMedicationOrderRepository, MedicationOrderRepository>();
        services.AddScoped<InpatientClinicalManager>();
        services.AddScoped<IInpatientClinicalManager>(sp => sp.GetRequiredService<InpatientClinicalManager>());
        services.AddScoped<IMedicationOrderLookup>(sp => sp.GetRequiredService<InpatientClinicalManager>());
        services.AddSingleton<IPermissionDefinitionProvider, ClinicalPermissionDefinitionProvider>();
        return services;
    }
}
