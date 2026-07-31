using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.VipApp;

namespace Core.Interfaces.Services;

// VIP Guest App — mobile-facing surface. All guest-scoped: the guest is
// resolved from the guest JWT, NOT passed in. guestId params below are the
// resolved caller unless noted.
public interface IVipAppService
{
    // ---- Auth (public) ----
    Task<ApiResponse<bool>> RequestOtpAsync(RequestOtpRequest request, CancellationToken ct);
    Task<ApiResponse<GuestAuthResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct);
    Task<ApiResponse<GuestAuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct);
    Task<ApiResponse<bool>> LogoutAsync(string refreshToken, CancellationToken ct);

    // ---- Events / selection ----
    Task<ApiResponse<List<GuestEventResponse>>> GetEventsAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<List<GuestSessionResponse>>> GetEventSessionsAsync(int guestId, Guid eventId, CancellationToken ct);
    Task<ApiResponse<bool>> SubmitSelectionAsync(int guestId, Guid eventId, SessionSelectionRequest request, CancellationToken ct);

    // ---- Agenda (upcoming actions: flight / check-in / transport) ----
    Task<ApiResponse<List<AgendaCardResponse>>> GetAgendaAsync(int guestId, Guid? eventId = null, CancellationToken ct = default);

    /// <summary>Just the dates this guest has a flight, stay or transfer on —
    /// what /itinerary?date= expects. Dates only, deliberately cheap.</summary>
    /// <param name="eventId">Only this event. Null = every event the guest is on.</param>
    Task<ApiResponse<List<DateOnly>>> GetItineraryDatesAsync(int guestId, Guid? eventId = null, CancellationToken ct = default);

    /// <summary>The guest's flights / transport / accommodation / sessions in one call.</summary>
    /// <param name="eventId">Only this event. Null = every event the guest is on.</param>
    /// <param name="date">Only what falls on this date. Null = the whole itinerary.</param>
    Task<ApiResponse<ItinerarySummaryResponse>> GetItineraryAsync(int guestId, Guid? eventId = null, DateOnly? date = null, CancellationToken ct = default);

    // ---- Travel ---- (eventId null on any of these = every event the guest is on)
    Task<ApiResponse<List<FlightBookingResponse>>> GetFlightsAsync(int guestId, Guid? eventId, CancellationToken ct);
    Task<ApiResponse<AccommodationResponse>> GetAccommodationAsync(int guestId, Guid? eventId, CancellationToken ct);
    Task<ApiResponse<TransportationResponse>> GetTransportationAsync(int guestId, Guid? eventId, CancellationToken ct);

    /// <summary>Guest requests a car — created with status "new" and no driver,
    /// waiting for a driver to accept it.</summary>
    Task<ApiResponse<TransportationResponse>> RequestTransportAsync(int guestId, TransportRequest request, CancellationToken ct);

    /// <summary>Guest cancels their own request — allowed only while it is still
    /// "new" (unclaimed). Once a driver has it (assigned or beyond) this returns
    /// 409 and the ride stands; dispatch cancels those instead.</summary>
    Task<ApiResponse<bool>> CancelTransportRequestAsync(int guestId, Guid transportId, CancellationToken ct);

    // ---- Sessions ----
    Task<ApiResponse<List<GuestSessionResponse>>> GetSessionsAsync(int guestId, Guid? eventId = null, CancellationToken ct = default);
    Task<ApiResponse<SessionDetailResponse>> GetSessionDetailAsync(int guestId, Guid sessionId, CancellationToken ct);

    // ---- Preferences ----
    Task<ApiResponse<GuestPreferencesResponse>> GetPreferencesAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<GuestPreferencesResponse>> UpdatePreferencesAsync(int guestId, GuestPreferencesResponse request, CancellationToken ct);

    // ---- Profile ----
    Task<ApiResponse<GuestProfileResponse>> GetProfileAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<GuestProfileResponse>> UpdateProfileAsync(int guestId, UpdateProfileRequest request, CancellationToken ct);
    Task<ApiResponse<bool>> UpdateSettingsAsync(int guestId, UpdateSettingsRequest request, CancellationToken ct);

    // Support chat lives entirely on ISupportChatService / SupportChatController now.

    // Notifications / devices live entirely on INotificationService / NotificationsController
    // now (see GetGuestNotificationsAsync et al.) — same move as support chat above.
}
