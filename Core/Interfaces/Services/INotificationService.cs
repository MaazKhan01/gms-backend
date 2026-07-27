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
    Task<ApiResponse<bool>> RegisterGuestDeviceAsync(int guestId, RegisterDeviceRequest request, CancellationToken ct = default);
}
