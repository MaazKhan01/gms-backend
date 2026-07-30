using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.VipApp;

namespace API.Controllers.v1;

// Device registration (push tokens) — split out of NotificationsController.
// Same route prefix ("api/v1/notifications") so existing clients (portal,
// driver app, VIP app) don't break on the move.
[Route("api/v1/notifications")]
[Authorize]
[ApiVersion("1.0")]
public class DevicesController : Controllers.BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentGuest _currentGuest;

    public DevicesController(INotificationService notificationService, ICurrentGuest currentGuest)
    {
        _notificationService = notificationService;
        _currentGuest = currentGuest;
    }

    // Kept for backward compatibility with the existing VIP app build — an
    // alias over RegisterDeviceAsync keyed by the guest's linked UserId.
    [HttpPost("guest/devices")]
    public async Task<IActionResult> RegisterGuestDevice([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var result = await _notificationService.RegisterGuestDeviceAsync(_currentGuest.GuestId, request, ct);
        return ToResponse(result);
    }

    // Any authenticated User (staff, driver, or a guest token, whose
    // NameIdentifier resolves to their own linked User).
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
