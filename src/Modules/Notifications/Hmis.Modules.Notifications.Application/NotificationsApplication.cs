using Hmis.BuildingBlocks.Application.Abstractions;
using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Security;
using Hmis.BuildingBlocks.Contracts.Identity;
using Hmis.BuildingBlocks.Contracts.Notifications;
using Hmis.Modules.Notifications.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hmis.Modules.Notifications.Application;

/// <summary>Assembly marker used to register validators and mapping configurations.</summary>
public sealed class NotificationsApplicationMarker;

public static class NotificationPermissions
{
    public const string Module = "Notifications";

    public const string SettingsManage = "Notifications.Settings.Manage";
}

public sealed class NotificationPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(NotificationPermissions.SettingsManage, NotificationPermissions.Module, "Switch notification types on or off for the hospital",
            [SystemRoles.HospitalAdministrator])
    ];
}

/// <summary>Who receives each type (shown on the settings page).</summary>
public static class NotificationCatalog
{
    public static readonly IReadOnlyList<(string Type, string Label, string Recipients)> All =
    [
        (NotificationTypes.AppointmentBooked, "Appointment booked", "The doctor"),
        (NotificationTypes.PatientAdmitted, "Patient admitted", "The attending doctor"),
        (NotificationTypes.DischargeOrdered, "Discharge ordered", "Staff who prepare the final bill"),
        (NotificationTypes.FinalBillReady, "Final bill ready", "Cashiers"),
        (NotificationTypes.PatientDischarged, "Patient discharged", "The attending doctor"),
        (NotificationTypes.DiscountApproval, "Discount waiting for approval", "Discount approvers"),
        (NotificationTypes.RefundApproval, "Refund waiting for approval", "Refund approvers")
    ];
}

public sealed record NotificationDto(Guid Id, string Type, string Title, string Message, string? Link, DateTime CreatedAt, bool IsRead);

public sealed record NotificationListDto(int UnreadCount, IReadOnlyList<NotificationDto> Items);

public sealed record NotificationSettingDto(string Type, string Label, string Recipients, bool IsEnabled);

public sealed record NotificationSettingUpdateDto
{
    public bool IsEnabled { get; init; }
}

public sealed class NotificationQuery
{
    public bool? UnreadOnly { get; set; }
    public int? Take { get; set; }
}

public interface INotificationsUnitOfWork : IUnitOfWork;

public interface INotificationRepository : IRepository<Notification>
{
    Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, bool unreadOnly, int take, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Guid userId, DateTime at, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken);
}

public interface INotificationSettingRepository : IRepository<NotificationSetting>
{
    Task<IReadOnlyList<NotificationSetting>> GetAllAsync(CancellationToken cancellationToken);

    Task<NotificationSetting?> GetByTypeAsync(string type, CancellationToken cancellationToken);
}

public interface INotificationManager
{
    Task<NotificationListDto> GetMineAsync(NotificationQuery query, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(CancellationToken cancellationToken);

    Task MarkReadAsync(Guid id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationSettingDto>> GetSettingsAsync(CancellationToken cancellationToken);

    Task<NotificationSettingDto> UpdateSettingAsync(string type, NotificationSettingUpdateDto request, CancellationToken cancellationToken);
}

/// <summary>The bell: each user reads and dismisses only their own notifications.</summary>
public sealed class NotificationManager(
    INotificationRepository notifications,
    INotificationSettingRepository settings,
    INotificationsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : INotificationManager
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<NotificationListDto> GetMineAsync(NotificationQuery query, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(query.Take ?? DefaultTake, 1, MaxTake);
        var list = await notifications.GetForUserAsync(UserId, query.UnreadOnly == true, take, cancellationToken);
        return new NotificationListDto(await notifications.CountUnreadAsync(UserId, cancellationToken),
            list.Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.Link, n.CreatedAt, n.ReadAt is not null)).ToList());
    }

    public Task<int> CountUnreadAsync(CancellationToken cancellationToken) => notifications.CountUnreadAsync(UserId, cancellationToken);

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null || notification.RecipientUserId != UserId)
        {
            throw new NotFoundException("Notification", id);
        }

        notification.MarkRead(dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task MarkAllReadAsync(CancellationToken cancellationToken) => notifications.MarkAllReadAsync(UserId, dateTime.UtcNow, cancellationToken);

    public async Task<IReadOnlyList<NotificationSettingDto>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var stored = (await settings.GetAllAsync(cancellationToken)).ToDictionary(s => s.Type, s => s.IsEnabled);
        return NotificationCatalog.All.Select(c => new NotificationSettingDto(c.Type, c.Label, c.Recipients, stored.GetValueOrDefault(c.Type, true))).ToList();
    }

    public async Task<NotificationSettingDto> UpdateSettingAsync(string type, NotificationSettingUpdateDto request, CancellationToken cancellationToken)
    {
        var entry = NotificationCatalog.All.FirstOrDefault(c => c.Type == type);
        if (entry.Type is null)
        {
            throw new NotFoundException("Notification type", type);
        }

        var setting = await settings.GetByTypeAsync(type, cancellationToken);
        if (setting is null)
        {
            await settings.AddAsync(NotificationSetting.Create(type, request.IsEnabled), cancellationToken);
        }
        else
        {
            setting.SetEnabled(request.IsEnabled);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NotificationSettingDto(entry.Type, entry.Label, entry.Recipients, request.IsEnabled);
    }
}

/// <summary>Creates notifications for other modules. Failures are logged and swallowed (the business action stands).</summary>
public sealed class NotificationService(
    INotificationRepository notifications,
    INotificationSettingRepository settings,
    INotificationsUnitOfWork unitOfWork,
    IUserDirectory users,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task NotifyUsersAsync(IReadOnlyCollection<Guid> userIds, NotificationMessage message, CancellationToken cancellationToken) =>
        SafelyAsync(message, _ => Task.FromResult(userIds), cancellationToken);

    public Task NotifyPermissionAsync(string permission, NotificationMessage message, CancellationToken cancellationToken) =>
        SafelyAsync(message, async ct => (IReadOnlyCollection<Guid>)await users.GetUserIdsWithPermissionAsync(permission, ct), cancellationToken);

    public Task NotifyEmployeeAsync(Guid employeeId, NotificationMessage message, CancellationToken cancellationToken) =>
        SafelyAsync(message, async ct => (IReadOnlyCollection<Guid>)await users.GetUserIdsForEmployeeAsync(employeeId, ct), cancellationToken);

    private async Task SafelyAsync(NotificationMessage message, Func<CancellationToken, Task<IReadOnlyCollection<Guid>>> recipients,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await settings.GetByTypeAsync(message.Type, cancellationToken) is { IsEnabled: false })
            {
                return;
            }

            var self = currentUser.UserId;
            var ids = (await recipients(cancellationToken)).Where(id => id != self).Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            var now = dateTime.UtcNow;
            await notifications.AddRangeAsync(ids.Select(id => Notification.Create(id, message.Type, message.Title, message.Message, message.Link, now)),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Notification {Type} could not be created", message.Type);
        }
    }
}
