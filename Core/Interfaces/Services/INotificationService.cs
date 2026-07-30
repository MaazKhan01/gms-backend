using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;
using Core.ViewModel.VipApp;

namespace Core.Interfaces.Services;

public interface INotificationService
{
    // ---- User-facing (admin/staff) — resolved from ICurrentUser ----
    Task<ApiResponse<PaginatedResponse<NotificationResponse>>> GetAllNotificationAsync(NotificationPagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<NotificationResponse>> GetNotificationByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkAllAsReadAsync(CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkSingleAsReadAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkSingleAsUnreadAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<int>> GetTotalCountAsync(CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteNotificationAsync(Guid id, CancellationToken ct = default);

    // Admin send — targets one/many users, a role, a permission, everyone, or
    // (separately, since guests aren't Users) one/many guests or all guests.
    // Gated by PermissionCodes.NotificationsSend at the controller.
    Task<ApiResponse<SendNotificationResult>> SendNotificationAsync(SendNotificationRequest request, CancellationToken ct = default);

    // ---- Guest-facing (VIP app, OTP login) — guestId passed in by the caller,
    // resolved from ICurrentGuest. Guests aren't Users (see GuestNotification),
    // so this is a distinct set of methods, not an overload of the above.
    // Moved here from IVipAppService — same precedent as support chat, which
    // now lives entirely on ISupportChatService/SupportChatController.
    Task<ApiResponse<List<GuestNotificationResponse>>> GetGuestNotificationsAsync(int guestId, PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<int>> GetGuestUnreadCountAsync(int guestId, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkGuestNotificationReadAsync(int guestId, Guid notificationId, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkAllGuestNotificationsReadAsync(int guestId, CancellationToken ct = default);

    // Kept for backward compatibility with the existing VIP app build — an
    // alias over RegisterDeviceAsync keyed by the guest's linked UserId.
    Task<ApiResponse<bool>> RegisterGuestDeviceAsync(int guestId, RegisterDeviceRequest request, CancellationToken ct = default);

    // ---- Device registration — any authenticated User (staff, driver, or
    // guest), resolved from ICurrentUser. See Device entity remarks. ----
    Task<ApiResponse<bool>> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct = default);

    // Updates fields (platform, model, OS/app version, notifications toggle,
    // device identifier) on the caller's already-registered Device row,
    // found by Token. Distinct from RegisterDeviceAsync's upsert-by-token.
    Task<ApiResponse<DeviceResponse>> UpdateDeviceAsync(UpdateDeviceRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeregisterDeviceAsync(string token, CancellationToken ct = default);

    // Existence check: does this exact token already have a Device row for the
    // current User? Lets a client skip re-registering (or detect a stale local
    // token) without a write. Scoped to the caller — never returns another
    // User's device even if the token string happened to match.
    Task<ApiResponse<DeviceResponse>> GetMyDeviceByTokenAsync(string token, CancellationToken ct = default);

    // Every device currently registered to the caller.
    Task<ApiResponse<List<DeviceResponse>>> GetMyDevicesAsync(CancellationToken ct = default);
}
