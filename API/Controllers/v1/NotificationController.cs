using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;

namespace API.Controllers.v1;

[Route("api/v1/notifications")]
[Authorize]
[ApiVersion("1.0")]
public class NotificationsController : Controllers.BaseApiController
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] NotificationPagedRequest request, CancellationToken ct)
    {
        var result = await _notificationService.GetAllNotificationAsync(request, ct);
        return ToResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.GetNotificationByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var result = await _notificationService.GetTotalCountAsync(ct);
        return ToResponse(result);
    }

    [HttpPut("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var result = await _notificationService.MarkAllAsReadAsync(ct);
        return ToResponse(result);
    }

    [HttpPut("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.MarkSingleAsReadAsync(id, ct);
        return ToResponse(result);
    }

    [HttpPut("{id:guid}/mark-unread")]
    public async Task<IActionResult> MarkAsUnread(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.MarkSingleAsUnreadAsync(id, ct);
        return ToResponse(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.DeleteNotificationAsync(id, ct);
        return ToResponse(result);
    }

    // Admin fan-out: one/many users, a role, a permission, or everyone —
    // and, separately (guests aren't Users), one/many guests or all guests.
    // See SendNotificationRequest for the six mutually-exclusive targets.
    [HttpPost("send")]
    [HasPermission(PermissionCodes.NotificationsSend)]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request, CancellationToken ct)
    {
        var result = await _notificationService.SendNotificationAsync(request, ct);
        return ToResponse(result);
    }
}
