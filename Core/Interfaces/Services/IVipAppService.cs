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
    Task<ApiResponse<List<AgendaCardResponse>>> GetAgendaAsync(int guestId, CancellationToken ct);

    /// <summary>The guest's flights / transport / accommodation / sessions in one call.</summary>
    /// <param name="eventId">Only this event. Null = every event the guest is on.</param>
    /// <param name="date">Only what falls on this date. Null = the whole itinerary.</param>
    Task<ApiResponse<ItinerarySummaryResponse>> GetItineraryAsync(int guestId, Guid? eventId = null, DateOnly? date = null, CancellationToken ct = default);

    // ---- Travel ----
    Task<ApiResponse<List<FlightLegResponse>>> GetFlightsAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<AccommodationResponse>> GetAccommodationAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<TransportationResponse>> GetTransportationAsync(int guestId, CancellationToken ct);

    // ---- Sessions ----
    Task<ApiResponse<List<GuestSessionResponse>>> GetSessionsAsync(int guestId, CancellationToken ct);
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
