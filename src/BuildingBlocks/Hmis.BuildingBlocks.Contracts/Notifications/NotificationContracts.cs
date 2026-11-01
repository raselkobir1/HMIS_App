namespace Hmis.BuildingBlocks.Contracts.Notifications;

// Contracts owned and implemented by the Notifications module.

/// <summary>In-app notification types (BRD 6.1). Each can be switched off by an administrator.</summary>
public static class NotificationTypes
{
    public const string AppointmentBooked = "AppointmentBooked";
    public const string PatientAdmitted = "PatientAdmitted";
    public const string DischargeOrdered = "DischargeOrdered";
    public const string FinalBillReady = "FinalBillReady";
    public const string PatientDischarged = "PatientDischarged";
    public const string DiscountApproval = "DiscountApproval";
    public const string RefundApproval = "RefundApproval";
}

/// <param name="Link">App route opened from the notification, e.g. "/ipd/admissions/{id}".</param>
public sealed record NotificationMessage(string Type, string Title, string Message, string? Link);

/// <summary>
/// Sends in-app notifications. Never throws: a failed notification is logged and must not undo the business action.
/// The signed-in user is never notified of their own action.
/// </summary>
public interface INotificationService
{
    Task NotifyUsersAsync(IReadOnlyCollection<Guid> userIds, NotificationMessage message, CancellationToken cancellationToken);

    /// <summary>Every active user holding the permission.</summary>
    Task NotifyPermissionAsync(string permission, NotificationMessage message, CancellationToken cancellationToken);

    /// <summary>The login(s) linked to an HR employee, e.g. a doctor.</summary>
    Task NotifyEmployeeAsync(Guid employeeId, NotificationMessage message, CancellationToken cancellationToken);
}
