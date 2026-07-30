using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.VipApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers.v1;

// ============================================================================
// VIP Guest App — single mobile-facing controller. All endpoints under
// api/v1/vip-app. Auth endpoints are public; everything else is guest-scoped.
//
// GuestId comes from ICurrentGuest, which resolves it from the OTP-login
// token's User.Id via Guests.UserId — NOT ICurrentUser, whose id is Users.Id.
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

    // ---------------- Agenda ----------------
    // Upcoming guest actions only: flights, hotel check-in, transport pickups.
    [HttpGet("agenda")]
    // eventId optional — omit it for every event the guest is on.
    public async Task<IActionResult> GetAgenda([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _vip.GetAgendaAsync(GuestId, eventId, ct));

    // ---------------- Itinerary ----------------
    // Dates only — the days this guest has a flight, stay or transfer on. Feed
    // any of them back as /itinerary?date=. eventId optional.
    [HttpGet("itinerary/dates")]
    public async Task<IActionResult> GetItineraryDates([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _vip.GetItineraryDatesAsync(GuestId, eventId, ct));

    // Flights / transport / accommodation / sessions in one call. eventId and
    // date are both optional — omit either to skip that filter.
    [HttpGet("itinerary")]
    public async Task<IActionResult> GetItinerary([FromQuery] Guid? eventId, [FromQuery] DateOnly? date, CancellationToken ct)
        => ToResponse(await _vip.GetItineraryAsync(GuestId, eventId, date, ct));

    // ---------------- Travel ----------------
    // eventId optional on all three — omit it for every event the guest is on.
    [HttpGet("flights")]
    public async Task<IActionResult> GetFlights([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _vip.GetFlightsAsync(GuestId, eventId, ct));

    [HttpGet("accommodation")]
    public async Task<IActionResult> GetAccommodation([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _vip.GetAccommodationAsync(GuestId, eventId, ct));

    [HttpGet("transportation")]
    public async Task<IActionResult> GetTransportation([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _vip.GetTransportationAsync(GuestId, eventId, ct));

    // Guest books a car: pickup/drop-off location, vehicle, times. Created with
    // status "new" and no driver until a driver accepts it.
    [HttpPost("transport-requests")]
    public async Task<IActionResult> RequestTransport([FromBody] TransportRequest request, CancellationToken ct)
        => ToResponse(await _vip.RequestTransportAsync(GuestId, request, ct));

    // ---------------- Sessions ----------------
    [HttpGet("sessions")]
    // eventId optional — omit it for every event the guest is on.
    public async Task<IActionResult> GetSessions([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _vip.GetSessionsAsync(GuestId, eventId, ct));

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

    // Notifications / devices now live on NotificationsController
    // (api/v1/notifications/guest/...) — same move as support chat above.
}
