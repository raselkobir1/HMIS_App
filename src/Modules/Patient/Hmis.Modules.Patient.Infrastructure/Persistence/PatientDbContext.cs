using System.Text.Json;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Patient.Application;
using Hmis.Modules.Patient.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientEntity = Hmis.Modules.Patient.Domain.Entities.Patient;

namespace Hmis.Modules.Patient.Infrastructure.Persistence;

/// <summary>Owns the "patient" schema.</summary>
public sealed class PatientDbContext(DbContextOptions<PatientDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "patient";

    public override string Schema => SchemaName;

    public override string ModuleName => "Patient";

    public DbSet<PatientEntity> Patients => Set<PatientEntity>();
    public DbSet<PatientAllergy> Allergies => Set<PatientAllergy>();
    public DbSet<PatientMedicalHistory> MedicalHistories => Set<PatientMedicalHistory>();
    public DbSet<PatientDocument> Documents => Set<PatientDocument>();
    public DbSet<PatientMergeRequest> MergeRequests => Set<PatientMergeRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trigram index for fast fuzzy name search and duplicate detection.
        modelBuilder.HasPostgresExtension("pg_trgm");
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class PatientUnitOfWork(PatientDbContext context) : UnitOfWork<PatientDbContext>(context), IPatientUnitOfWork;

public sealed class PatientConfiguration : IEntityTypeConfiguration<PatientEntity>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<PatientEntity> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MRNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.MRNumber).IsUnique();

        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.FullName).HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.Property(x => x.FullNameBangla).HasMaxLength(150);
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(x => new { x.DateOfBirth, x.Gender });
        builder.Property(x => x.BloodGroup).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.MobileNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.MobileNumber);
        builder.Property(x => x.Email).HasMaxLength(256);

        // Addresses and contacts are stored as JSON so changes appear in full in the audit trail.
        builder.Property(x => x.PresentAddress).HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, Json),
            v => JsonSerializer.Deserialize<Address>(v, Json) ?? Address.Empty);
        builder.Property(x => x.PermanentAddress).HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, Json),
            v => JsonSerializer.Deserialize<Address>(v, Json) ?? Address.Empty);
        builder.Property(x => x.EmergencyContacts).HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, Json),
            v => JsonSerializer.Deserialize<List<EmergencyContact>>(v, Json) ?? new List<EmergencyContact>(),
            new ValueComparer<List<EmergencyContact>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, c) => HashCode.Combine(hash, c)),
                v => v.ToList()));

        builder.Property(x => x.IdentificationType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IdentificationNumber).HasMaxLength(30);
        builder.HasIndex(x => new { x.IdentificationType, x.IdentificationNumber })
            .IsUnique()
            .HasFilter("\"IdentificationNumber\" IS NOT NULL");

        builder.Property(x => x.GuardianName).HasMaxLength(150);
        builder.Property(x => x.GuardianRelation).HasMaxLength(50);
        builder.Property(x => x.PhotoPath).HasMaxLength(300);
        builder.Property(x => x.PhotoContentType).HasMaxLength(50);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Status);
        builder.HasOne<PatientEntity>().WithMany().HasForeignKey(x => x.MergedIntoPatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PatientAllergyConfiguration : IEntityTypeConfiguration<PatientAllergy>
{
    public void Configure(EntityTypeBuilder<PatientAllergy> builder)
    {
        builder.ToTable("Allergies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Allergen).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reaction).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.PatientId);
        builder.HasOne<PatientEntity>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PatientMedicalHistoryConfiguration : IEntityTypeConfiguration<PatientMedicalHistory>
{
    public void Configure(EntityTypeBuilder<PatientMedicalHistory> builder)
    {
        builder.ToTable("MedicalHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.PatientId);
        builder.HasOne<PatientEntity>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PatientDocumentConfiguration : IEntityTypeConfiguration<PatientDocument>
{
    public void Configure(EntityTypeBuilder<PatientDocument> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Title).HasMaxLength(150).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.PatientId, x.IsDeleted });
        builder.HasOne<PatientEntity>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PatientMergeRequestConfiguration : IEntityTypeConfiguration<PatientMergeRequest>
{
    public void Configure(EntityTypeBuilder<PatientMergeRequest> builder)
    {
        builder.ToTable("MergeRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReviewNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        // At most one pending request per duplicate record (also checked by the manager).
        builder.HasIndex(x => x.SourcePatientId, "IX_MergeRequests_SourcePatientId_Pending")
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");

        builder.HasOne<PatientEntity>().WithMany().HasForeignKey(x => x.SourcePatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PatientEntity>().WithMany().HasForeignKey(x => x.TargetPatientId).OnDelete(DeleteBehavior.Restrict);
    }
}
