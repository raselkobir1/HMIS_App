using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Notifications;
using Hmis.BuildingBlocks.Infrastructure;
using Hmis.BuildingBlocks.Infrastructure.Persistence;
using Hmis.Modules.Notifications.Application;
using Hmis.Modules.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hmis.Modules.Notifications.Infrastructure;

/// <summary>Owns the "notify" schema.</summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "notify";

    public override string Schema => SchemaName;

    public override string ModuleName => "Notifications";

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationSetting> Settings => Set<NotificationSetting>();
}

public sealed class NotificationsUnitOfWork(NotificationsDbContext context) : UnitOfWork<NotificationsDbContext>(context), INotificationsUnitOfWork;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Link).HasMaxLength(300);
        builder.HasIndex(x => new { x.RecipientUserId, x.CreatedAt });
        builder.HasIndex(x => x.RecipientUserId).HasFilter("\"ReadAt\" IS NULL").HasDatabaseName("IX_Notifications_Unread");
    }
}

public sealed class NotificationSettingConfiguration : IEntityTypeConfiguration<NotificationSetting>
{
    public void Configure(EntityTypeBuilder<NotificationSetting> builder)
    {
        builder.ToTable("NotificationSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Type).IsUnique();
    }
}

public sealed class NotificationRepository(NotificationsDbContext context)
    : BaseRepository<Notification, NotificationsDbContext>(context), INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, bool unreadOnly, int take, CancellationToken cancellationToken) =>
        await Set.AsNoTracking()
            .Where(n => n.RecipientUserId == userId && (!unreadOnly || n.ReadAt == null))
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Set.CountAsync(n => n.RecipientUserId == userId && n.ReadAt == null, cancellationToken);

    public Task<int> MarkAllReadAsync(Guid userId, DateTime at, CancellationToken cancellationToken) =>
        Set.Where(n => n.RecipientUserId == userId && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, at), cancellationToken);

    public Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken) =>
        Set.AddRangeAsync(notifications, cancellationToken);
}

public sealed class NotificationSettingRepository(NotificationsDbContext context)
    : BaseRepository<NotificationSetting, NotificationsDbContext>(context), INotificationSettingRepository
{
    public async Task<IReadOnlyList<NotificationSetting>> GetAllAsync(CancellationToken cancellationToken) =>
        await Set.ToListAsync(cancellationToken);

    public Task<NotificationSetting?> GetByTypeAsync(string type, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(s => s.Type == type, cancellationToken);
}

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration, NotificationsDbContext.SchemaName);
        services.AddScoped<INotificationsUnitOfWork, NotificationsUnitOfWork>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationSettingRepository, NotificationSettingRepository>();
        services.AddScoped<INotificationManager, NotificationManager>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IPermissionDefinitionProvider, NotificationPermissionDefinitionProvider>();
        return services;
    }
}
