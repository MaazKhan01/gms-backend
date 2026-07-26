using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.VipApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers.v1;

// ============================================================================
// VIP Guest App — single mobile-facing controller. All endpoints under
// api/v1/vip-app. Auth endpoints are public; everything else is guest-scoped.
//
// GuestId comes from ICurrentGuest (the "guestId" JWT claim issued by the
// OTP-login flow), NOT ICurrentUser — a guest is not a system user.
// ============================================================================
[Route("api/v1/vip-app")]
[Authorize]
[ApiVersion("1.0")]
public class VipAppController(IVipAppService _vip, ICurrentGuest _currentGuest) : Controllers.BaseApiController
{
    private int GuestId => _currentGuest.GuestId;

    // ---------------- Auth (public) ----------------
    [HttpPost("auth/request-otp"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request, CancellationToken ct)
        => ToResponse(await _vip.RequestOtpAsync(request, ct));

    [HttpPost("auth/verify-otp"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
        => ToResponse(await _vip.VerifyOtpAsync(request, ct));

    [HttpPost("auth/refresh"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => ToResponse(await _vip.RefreshTokenAsync(request.RefreshToken, ct));

    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
        => ToResponse(await _vip.LogoutAsync(request.RefreshToken, ct));

    // ---------------- Events / selection ----------------
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(CancellationToken ct)
        => ToResponse(await _vip.GetEventsAsync(GuestId, ct));

    [HttpGet("events/{eventId:guid}/sessions")]
    public async Task<IActionResult> GetEventSessions(Guid eventId, CancellationToken ct)
        => ToResponse(await _vip.GetEventSessionsAsync(GuestId, eventId, ct));

    //[HttpPost("events/{eventId:guid}/selection")]
    //public async Task<IActionResult> SubmitSelection(Guid eventId, [FromBody] SessionSelectionRequest request, CancellationToken ct)
    //    => ToResponse(await _vip.SubmitSelectionAsync(GuestId, eventId, request, ct));

    // ---------------- Home / itinerary ----------------
    [HttpGet("home")]
    public async Task<IActionResult> GetHome(CancellationToken ct)
        => ToResponse(await _vip.GetHomeAsync(GuestId, ct));

    [HttpGet("itinerary")]
    public async Task<IActionResult> GetItinerary(CancellationToken ct)
        => ToResponse(await _vip.GetItineraryAsync(GuestId, ct));

    // ---------------- Travel ----------------
    [HttpGet("flights")]
    public async Task<IActionResult> GetFlights(CancellationToken ct)
        => ToResponse(await _vip.GetFlightsAsync(GuestId, ct));

    [HttpGet("accommodation")]
    public async Task<IActionResult> GetAccommodation(CancellationToken ct)
        => ToResponse(await _vip.GetAccommodationAsync(GuestId, ct));

    [HttpGet("transportation")]
    public async Task<IActionResult> GetTransportation(CancellationToken ct)
        => ToResponse(await _vip.GetTransportationAsync(GuestId, ct));

    // ---------------- Sessions ----------------
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
        => ToResponse(await _vip.GetSessionsAsync(GuestId, ct));

    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> GetSessionDetail(Guid id, CancellationToken ct)
        => ToResponse(await _vip.GetSessionDetailAsync(GuestId, id, ct));

    // ---------------- Preferences ----------------
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
        => ToResponse(await _vip.GetPreferencesAsync(GuestId, ct));

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] GuestPreferencesResponse request, CancellationToken ct)
        => ToResponse(await _vip.UpdatePreferencesAsync(GuestId, request, ct));

    // ---------------- Profile ----------------
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
        => ToResponse(await _vip.GetProfileAsync(GuestId, ct));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
        => ToResponse(await _vip.UpdateProfileAsync(GuestId, request, ct));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
        => ToResponse(await _vip.UpdateSettingsAsync(GuestId, request, ct));

    // ---------------- Notifications / devices ----------------
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _vip.GetNotificationsAsync(GuestId, request, ct));

    [HttpGet("notifications/count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
        => ToResponse(await _vip.GetUnreadCountAsync(GuestId, ct));

    [HttpPut("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
        => ToResponse(await _vip.MarkNotificationReadAsync(GuestId, id, ct));

    [HttpPut("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct)
        => ToResponse(await _vip.MarkAllNotificationsReadAsync(GuestId, ct));

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken ct)
        => ToResponse(await _vip.RegisterDeviceAsync(GuestId, request, ct));
}
