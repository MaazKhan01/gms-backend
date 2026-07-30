using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;
using Core.ViewModel.VipApp;

namespace API.Controllers.v1;

// User/admin notifications (this file) AND guest notifications (the "guest/"
// sub-routes below, moved here from VipAppController — same precedent as
// support chat) both live on this controller. They're deliberately NOT the
// same routes: a guest is not a row in the Users table (OTP login, separate
// GuestNotification table, resolved via ICurrentGuest not ICurrentUser), so
// merging them into one set of endpoints would require conflating two
// distinct identities and audiences.
[Route("api/v1/notifications")]
[Authorize]
[ApiVersion("1.0")]
public class NotificationsController : Controllers.BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentGuest _currentGuest;

    public NotificationsController(INotificationService notificationService, ICurrentGuest currentGuest)
    {
        _notificationService = notificationService;
        _currentGuest = currentGuest;
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

    // ============================================================
    // Guest-facing (VIP app, OTP login) — GuestId resolved from ICurrentGuest,
    // NOT ICurrentUser. Moved from VipAppController's "Notifications / devices"
    // section (same precedent as support chat, extracted earlier).
    // ============================================================

    [HttpGet("guest")]
    public async Task<IActionResult> GetGuestNotifications([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _notificationService.GetGuestNotificationsAsync(_currentGuest.GuestId, request, ct);
        return ToResponse(result);
    }

    [HttpGet("guest/count")]
    public async Task<IActionResult> GetGuestUnreadCount(CancellationToken ct)
    {
        var result = await _notificationService.GetGuestUnreadCountAsync(_currentGuest.GuestId, ct);
        return ToResponse(result);
    }

    [HttpPut("guest/{id:guid}/mark-read")]
    public async Task<IActionResult> MarkGuestNotificationRead(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.MarkGuestNotificationReadAsync(_currentGuest.GuestId, id, ct);
        return ToResponse(result);
    }

    [HttpPut("guest/mark-all-read")]
    public async Task<IActionResult> MarkAllGuestNotificationsRead(CancellationToken ct)
    {
        var result = await _notificationService.MarkAllGuestNotificationsReadAsync(_currentGuest.GuestId, ct);
        return ToResponse(result);
    }

    [HttpPost("guest/devices")]
    public async Task<IActionResult> RegisterGuestDevice([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var result = await _notificationService.RegisterGuestDeviceAsync(_currentGuest.GuestId, request, ct);
        return ToResponse(result);
    }

    // ============================================================
    // Device registration — any authenticated User (staff, driver, or a guest
    // token, whose NameIdentifier now resolves to their own linked User —
    // see VipAppService.BuildAccessToken). "guest/devices" above is kept only
    // for the existing VIP app build; new clients (driver app included) should
    // use these instead.
    // ============================================================

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken ct)
        => ToResponse(await _notificationService.RegisterDeviceAsync(request, ct));

    // Update fields on the caller's already-registered device row (found by
    // Token) — platform, model, OS/app version, notifications toggle. Use
    // POST devices instead when the token itself changed.
    [HttpPut("devices")]
    public async Task<IActionResult> UpdateDevice([FromBody] UpdateDeviceRequest request, CancellationToken ct)
        => ToResponse(await _notificationService.UpdateDeviceAsync(request, ct));

    // Every device currently registered to the caller.
    [HttpGet("devices")]
    public async Task<IActionResult> GetMyDevices(CancellationToken ct)
        => ToResponse(await _notificationService.GetMyDevicesAsync(ct));

    // Existence check for one specific token — 200 + the device if this exact
    // token is already registered to the caller, 404 otherwise. Lets a client
    // decide whether to call POST devices at all instead of blindly upserting.
    [HttpGet("devices/check")]
    public async Task<IActionResult> CheckMyDevice([FromQuery] string token, CancellationToken ct)
        => ToResponse(await _notificationService.GetMyDeviceByTokenAsync(token, ct));

    // Token via query string, not a route segment — FCM tokens can contain
    // characters ('/', '+') that don't round-trip safely through a URL path.
    [HttpDelete("devices")]
    public async Task<IActionResult> DeregisterDevice([FromQuery] string token, CancellationToken ct)
        => ToResponse(await _notificationService.DeregisterDeviceAsync(token, ct));
}
