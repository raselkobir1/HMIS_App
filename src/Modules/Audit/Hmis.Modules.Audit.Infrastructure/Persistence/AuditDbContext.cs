using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hmis.Modules.Audit.Infrastructure.Persistence;

/// <summary>Owns the "audit" schema. Used for reading and migrations; rows are written by AuditLogWriter.</summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "audit";
    public const string TableName = "AuditLogs";

    public override string Schema => SchemaName;

    public override string ModuleName => "Audit";

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(AuditDbContext.TableName);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserName).HasMaxLength(100);
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.TraceId).HasMaxLength(64);
        builder.Property(x => x.OldValues).HasColumnType("jsonb");
        builder.Property(x => x.NewValues).HasColumnType("jsonb");

        builder.HasIndex(x => x.Timestamp).IsDescending();
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
        builder.HasIndex(x => new { x.UserId, x.Timestamp });
        builder.HasIndex(x => new { x.Module, x.Timestamp });
    }
}
