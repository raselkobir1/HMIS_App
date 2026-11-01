using Hmis.BuildingBlocks.Domain.Abstractions;
using Hmis.BuildingBlocks.Domain.Entities;

namespace Hmis.Modules.Notifications.Domain.Entities;

/// <summary>One in-app message for one user. Not audited: it only points to records that are audited themselves.</summary>
public sealed class Notification : BaseEntity, IAggregateRoot
{
    private Notification() { }

    public Guid RecipientUserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public static Notification Create(Guid recipientUserId, string type, string title, string message, string? link, DateTime at) =>
        new()
        {
            RecipientUserId = recipientUserId,
            Type = type,
            Title = Truncate(title, 150),
            Message = Truncate(message, 500),
            Link = link,
            CreatedAt = at
        };

    public void MarkRead(DateTime at) => ReadAt ??= at;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

/// <summary>Administrator switch per notification type (BRD Module 01: notification settings). Missing row = enabled.</summary>
[Audited]
public sealed class NotificationSetting : AuditableEntity, IAggregateRoot
{
    private NotificationSetting() { }

    public string Type { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public static NotificationSetting Create(string type, bool isEnabled) => new() { Type = type, IsEnabled = isEnabled };

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
}
