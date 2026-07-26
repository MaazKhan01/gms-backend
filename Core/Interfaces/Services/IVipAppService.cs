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

    // ---- Home / itinerary ----
    Task<ApiResponse<HomeResponse>> GetHomeAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<List<ItineraryItemResponse>>> GetItineraryAsync(int guestId, CancellationToken ct);

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

    // ---- Notifications / devices ----
    Task<ApiResponse<List<GuestNotificationResponse>>> GetNotificationsAsync(int guestId, PagedRequest request, CancellationToken ct);
    Task<ApiResponse<int>> GetUnreadCountAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<bool>> MarkNotificationReadAsync(int guestId, Guid notificationId, CancellationToken ct);
    Task<ApiResponse<bool>> MarkAllNotificationsReadAsync(int guestId, CancellationToken ct);
    Task<ApiResponse<bool>> RegisterDeviceAsync(int guestId, RegisterDeviceRequest request, CancellationToken ct);
}
