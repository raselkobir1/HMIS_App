using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Notifications.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Notifications.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class NotificationsPresentationMarker;

/// <summary>The signed-in user's own notifications (no permission needed beyond signing in).</summary>
[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController(INotificationManager manager) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<NotificationListDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] NotificationQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetMineAsync(query, cancellationToken));

    [HttpGet("unread-count")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CountUnread(CancellationToken cancellationToken) =>
        OkResponse(await manager.CountUnreadAsync(cancellationToken));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await manager.MarkReadAsync(id, cancellationToken);
        return OkResponse("Marked as read.");
    }

    [HttpPost("read-all")]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await manager.MarkAllReadAsync(cancellationToken);
        return OkResponse("All notifications marked as read.");
    }

    [HttpGet("settings")]
    [HasPermission(NotificationPermissions.SettingsManage)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<NotificationSettingDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken) =>
        OkResponse(await manager.GetSettingsAsync(cancellationToken));

    [HttpPut("settings/{type}")]
    [HasPermission(NotificationPermissions.SettingsManage)]
    [ProducesResponseType<ApiResponse<NotificationSettingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSetting(string type, [FromBody] NotificationSettingUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateSettingAsync(type, request, cancellationToken), "Notification setting saved.");
}
