using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.DelegateApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers.v1;

// ============================================================================
// Delegate App — single mobile-facing controller. All endpoints under
// api/v1/delegate-app. Auth endpoints are public; everything else is guest-scoped.
//
// GuestId comes from ICurrentGuest, which resolves it from the OTP-login
// token's User.Id via Guests.UserId — NOT ICurrentUser, whose id is Users.Id.
// ============================================================================
[Route("api/v1/delegate-app")]
[Authorize]
[ApiVersion("1.0")]
public class DelegateAppController(IDelegateAppService _delegate, ICurrentGuest _currentGuest) : Controllers.BaseApiController, IActionFilter
{
    private int GuestId => _currentGuest.GuestId;

    // Delegates only: a staff/driver token on a non-anonymous endpoint resolves to
    // GuestId 0 (see CurrentGuest) and is turned away here, before any action runs.
    // [NonAction] on both: public methods on a controller are actions by default,
    // and a verb-less action breaks OpenAPI generation ("Unsupported HTTP method").
    [NonAction]
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var anonymous = context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (!anonymous && GuestId == 0)
            context.Result = ToResponse(ApiResponse<bool>.ForbiddenResponse("Only delegates can use this app"));
    }

    [NonAction]
    public void OnActionExecuted(ActionExecutedContext context) { }

    // ---------------- Auth (public) ----------------
    [HttpPost("auth/request-otp"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request, CancellationToken ct)
        => ToResponse(await _delegate.RequestOtpAsync(request, ct));

    [HttpPost("auth/verify-otp"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
        => ToResponse(await _delegate.VerifyOtpAsync(request, ct));

    [HttpPost("auth/refresh"), AllowAnonymous, EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => ToResponse(await _delegate.RefreshTokenAsync(request.RefreshToken, ct));

    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
        => ToResponse(await _delegate.LogoutAsync(request.RefreshToken, ct));

    // ---------------- Events / selection ----------------
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(CancellationToken ct)
        => ToResponse(await _delegate.GetEventsAsync(GuestId, ct));

    [HttpGet("events/{eventId:guid}/sessions")]
    public async Task<IActionResult> GetEventSessions(Guid eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetEventSessionsAsync(GuestId, eventId, ct));

    //[HttpPost("events/{eventId:guid}/selection")]
    //public async Task<IActionResult> SubmitSelection(Guid eventId, [FromBody] SessionSelectionRequest request, CancellationToken ct)
    //    => ToResponse(await _delegate.SubmitSelectionAsync(GuestId, eventId, request, ct));

    // ---------------- Agenda ----------------
    // Upcoming guest actions only: flights, hotel check-in, transport pickups.
    [HttpGet("agenda")]
    // eventId optional — omit it for every event the guest is on.
    public async Task<IActionResult> GetAgenda([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetAgendaAsync(GuestId, eventId, ct));

    // ---------------- Itinerary ----------------
    // Dates only — the days this guest has a flight, stay or transfer on. Feed
    // any of them back as /itinerary?date=. eventId optional.
    [HttpGet("itinerary/dates")]
    public async Task<IActionResult> GetItineraryDates([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetItineraryDatesAsync(GuestId, eventId, ct));

    // Flights / transport / accommodation / sessions in one call. eventId and
    // date are both optional — omit either to skip that filter.
    [HttpGet("itinerary")]
    public async Task<IActionResult> GetItinerary([FromQuery] Guid? eventId, [FromQuery] DateOnly? date, CancellationToken ct)
        => ToResponse(await _delegate.GetItineraryAsync(GuestId, eventId, date, ct));

    // ---------------- Travel ----------------
    // eventId optional on all three — omit it for every event the guest is on.
    [HttpGet("flights")]
    public async Task<IActionResult> GetFlights([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetFlightsAsync(GuestId, eventId, ct));

    [HttpGet("accommodation")]
    public async Task<IActionResult> GetAccommodation([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetAccommodationAsync(GuestId, eventId, ct));

    // Today's rides only, as a list. Everything else is on /transportation/bookings.
    [HttpGet("transportation/today")]
    public async Task<IActionResult> GetTodayTransportation([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetTodayTransportationAsync(GuestId, eventId, ct));

    // { upcoming: [...], history: [...] } — today excluded, it has its own call.
    [HttpGet("transportation/bookings")]
    public async Task<IActionResult> GetTransportationBookings([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetTransportationBookingsAsync(GuestId, eventId, ct));

    // Guest books a car: pickup/drop-off location, vehicle, times. Created with
    // status "new" and no driver until a driver accepts it.
    [HttpPost("transport-requests")]
    public async Task<IActionResult> RequestTransport([FromBody] TransportRequest request, CancellationToken ct)
        => ToResponse(await _delegate.RequestTransportAsync(GuestId, request, ct));

    // Guest drops their own request while it's still unclaimed ("new"). Once a
    // driver has accepted it (assigned onwards) this 409s — the guest can't pull
    // a job out from under a driver who is already on the way.
    [HttpPost("transport-requests/cancel")]
    public async Task<IActionResult> CancelTransportRequest([FromBody] CancelTransportRequest request, CancellationToken ct)
        => ToResponse(await _delegate.CancelTransportRequestAsync(GuestId, request.Id, ct));

    // Poll target for the "finding a chauffeur" screen: status flips from "new"
    // to assigned with Driver/Vehicle filled in once a driver accepts.
    [HttpGet("transport-requests/{id:guid}")]
    public async Task<IActionResult> GetTransportRequest(Guid id, CancellationToken ct)
        => ToResponse(await _delegate.GetTransportRequestAsync(GuestId, id, ct));

    // { upcoming, history } — every stay, unlike /accommodation (first one only).
    [HttpGet("stays")]
    public async Task<IActionResult> GetStays([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetStaysAsync(GuestId, eventId, ct));

    // ---------------- Mission (one participation) ----------------
    // Hero card, coordinator, next up, visa stepper, readiness checklist,
    // journal/report status, unread count — the whole Home tab in one call.
    [HttpGet("missions/{eventId:guid}/home")]
    public async Task<IActionResult> GetMissionHome(Guid eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetMissionHomeAsync(GuestId, eventId, ct));

    // Transport location picker: their hotels, mission venues, airports on their
    // flights, and places their rides have used. Full search: GET /lookup/locations.
    [HttpGet("missions/{eventId:guid}/places")]
    public async Task<IActionResult> GetPlaces(Guid eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetPlacesAsync(GuestId, eventId, ct));

    // Contracted hotels — the "change hotel" picker on a stay change request.
    [HttpGet("missions/{eventId:guid}/hotels")]
    public async Task<IActionResult> GetMissionHotels(Guid eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetMissionHotelsAsync(GuestId, eventId, ct));

    // ---------------- Booking change requests ----------------
    // "Request a change" on a flight or stay. Shows as "With coordinator" until
    // the portal marks it done / declined (PUT /api/v1/travel/change-requests/{id}).
    [HttpPost("change-requests")]
    public async Task<IActionResult> CreateChangeRequest([FromBody] ChangeRequestCreate request, CancellationToken ct)
        => ToResponse(await _delegate.CreateChangeRequestAsync(GuestId, request, ct));

    [HttpGet("change-requests")]
    public async Task<IActionResult> GetChangeRequests([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetChangeRequestsAsync(GuestId, eventId, ct));

    [HttpGet("missions/{eventId:guid}/accreditation")]
    public async Task<IActionResult> GetAccreditation(Guid eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetAccreditationAsync(GuestId, eventId, ct));

    // ---------------- Help requests ----------------
    // "Need help" sheet → an Incident (RaisedVia = app) on the portal board, and a
    // notification to the coordinator who nominated them.
    [HttpPost("help-requests")]
    public async Task<IActionResult> RaiseHelpRequest([FromBody] HelpRequest request, CancellationToken ct)
        => ToResponse(await _delegate.RaiseHelpRequestAsync(GuestId, request, ct));

    [HttpGet("help-requests")]
    public async Task<IActionResult> GetHelpRequests([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetHelpRequestsAsync(GuestId, eventId, ct));

    // ---------------- Mission journal ----------------
    [HttpGet("missions/{eventId:guid}/journal")]
    public async Task<IActionResult> GetJournal(Guid eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetJournalAsync(GuestId, eventId, ct));

    // multipart/form-data. Video is the largest allowed type (200 MB), so the
    // request ceiling sits just above it; the per-type limit is checked inside.
    [HttpPost("missions/{eventId:guid}/journal")]
    [RequestSizeLimit(210L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 210L * 1024 * 1024)]
    public async Task<IActionResult> AddJournalEntry(Guid eventId, [FromForm] CreateJournalEntryRequest request, CancellationToken ct)
        => ToResponse(await _delegate.AddJournalEntryAsync(GuestId, eventId, request, ct));

    // Tick / untick for the post-mission report.
    [HttpPatch("journal/{id:guid}")]
    public async Task<IActionResult> UpdateJournalEntry(Guid id, [FromBody] UpdateJournalEntryRequest request, CancellationToken ct)
        => ToResponse(await _delegate.UpdateJournalEntryAsync(GuestId, id, request, ct));

    [HttpDelete("journal/{id:guid}")]
    public async Task<IActionResult> DeleteJournalEntry(Guid id, CancellationToken ct)
        => ToResponse(await _delegate.DeleteJournalEntryAsync(GuestId, id, ct));

    // ---------------- Post-mission report ----------------
    [HttpGet("missions/{eventId:guid}/report")]
    public async Task<IActionResult> GetReport(Guid eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetReportAsync(GuestId, eventId, ct));

    // submit=false saves a draft; true submits it to the coordinator.
    [HttpPut("missions/{eventId:guid}/report")]
    public async Task<IActionResult> SaveReport(Guid eventId, [FromBody] SaveDelegateReportRequest request, CancellationToken ct)
        => ToResponse(await _delegate.SaveReportAsync(GuestId, eventId, request, ct));

    // ---------------- Sessions ----------------
    [HttpGet("sessions")]
    // eventId optional — omit it for every event the guest is on.
    public async Task<IActionResult> GetSessions([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _delegate.GetSessionsAsync(GuestId, eventId, ct));

    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> GetSessionDetail(Guid id, CancellationToken ct)
        => ToResponse(await _delegate.GetSessionDetailAsync(GuestId, id, ct));

    // ---------------- Preferences ----------------
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
        => ToResponse(await _delegate.GetPreferencesAsync(GuestId, ct));

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] GuestPreferencesResponse request, CancellationToken ct)
        => ToResponse(await _delegate.UpdatePreferencesAsync(GuestId, request, ct));

    // ---------------- Profile ----------------
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
        => ToResponse(await _delegate.GetProfileAsync(GuestId, ct));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
        => ToResponse(await _delegate.UpdateProfileAsync(GuestId, request, ct));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
        => ToResponse(await _delegate.UpdateSettingsAsync(GuestId, request, ct));

    // Notifications / devices now live on NotificationsController
    // (api/v1/notifications/guest/...) — same move as support chat above.
}
