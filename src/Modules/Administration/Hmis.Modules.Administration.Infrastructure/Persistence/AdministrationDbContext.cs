using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Administration.Application.Interfaces;
using Hmis.Modules.Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hmis.Modules.Administration.Infrastructure.Persistence;

/// <summary>Owns the "admin" schema.</summary>
public sealed class AdministrationDbContext(DbContextOptions<AdministrationDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "admin";

    public override string Schema => SchemaName;

    public override string ModuleName => "Administration";

    public DbSet<HospitalProfile> HospitalProfiles => Set<HospitalProfile>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
}

public sealed class AdministrationUnitOfWork(AdministrationDbContext context)
    : UnitOfWork<AdministrationDbContext>(context), IAdministrationUnitOfWork;

public sealed class HospitalProfileConfiguration : IEntityTypeConfiguration<HospitalProfile>
{
    public void Configure(EntityTypeBuilder<HospitalProfile> builder)
    {
        builder.ToTable("HospitalProfile");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameBangla).HasMaxLength(200);
        builder.Property(x => x.ShortName).HasMaxLength(50);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Website).HasMaxLength(256);
        builder.Property(x => x.RegistrationNumber).HasMaxLength(100);
        builder.Property(x => x.LicenseNumber).HasMaxLength(100);
        builder.Property(x => x.TaxId).HasMaxLength(50);
        builder.Property(x => x.OpeningHours).HasMaxLength(500);
        builder.Property(x => x.LogoPath).HasMaxLength(300);
        builder.Property(x => x.LogoContentType).HasMaxLength(50);
    }
}

public sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("SystemSettings", t =>
            t.HasCheckConstraint("CK_SystemSettings_FinancialYearStartMonth", "\"FinancialYearStartMonth\" BETWEEN 1 AND 12"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CurrencySymbol).HasMaxLength(5).IsRequired();
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.ParentDepartmentId);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.ToTable("Designations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("NumberSequences", t =>
            t.HasCheckConstraint("CK_NumberSequences_CurrentValue", "\"CurrentValue\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(10).IsRequired();
        builder.Property(x => x.YearFormat).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ResetPolicy).HasConversion<string>().HasMaxLength(20);
    }
}
