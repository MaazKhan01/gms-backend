using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Noification;
using Core.ViewModel.VipApp;

namespace Core.Interfaces.Services;

// The single reusable engine for creating + realtime-pushing notifications.
// Internal-id based (callers are other backend services that already hold
// int ids) — public-Guid-facing endpoints (NotificationService.SendNotificationAsync)
// resolve down to int ids before calling in here. Every method persists the
// Notification/GuestNotification row(s) first, then best-effort pushes over
// SignalR via IPushNotificationProvider — a dropped push never loses data,
// the DB row is the source of truth and the client can always re-fetch.
public interface INotificationManagerService
{
    // Legacy/simple entrypoint: an ephemeral all-clients broadcast (Topic) and/or
    // a single persisted user notification. Kept for backward compatibility.
    Task SendAlert(SendAlertRequest input);

    Task<NotificationResponse> SendToUserAsync(int userId, NotificationContent content, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationResponse>> SendToUsersAsync(IEnumerable<int> userIds, NotificationContent content, CancellationToken ct = default);

    // Every active user holding the given role.
    Task<IReadOnlyList<NotificationResponse>> SendToRoleAsync(string roleCode, NotificationContent content, CancellationToken ct = default);

    // Every active user holding the given permission (e.g. "notify all support agents").
    Task<IReadOnlyList<NotificationResponse>> SendToPermissionAsync(string permissionCode, NotificationContent content, CancellationToken ct = default);

    // Every active user in the system.
    Task<IReadOnlyList<NotificationResponse>> BroadcastToAllUsersAsync(NotificationContent content, CancellationToken ct = default);

    Task<GuestNotificationResponse> SendToGuestAsync(int guestId, NotificationContent content, CancellationToken ct = default);

    Task<IReadOnlyList<GuestNotificationResponse>> SendToGuestsAsync(IEnumerable<int> guestIds, NotificationContent content, CancellationToken ct = default);

    // Every Guest with NotificationsEnabled (their own in-app preference toggle —
    // see Guest.NotificationsEnabled / VipAppService.UpdateSettingsAsync). Direct
    // SendToGuest(s)Async targeting ignores this flag deliberately: an admin who
    // picked specific guests (or a 1:1 support reply) should still land, only a
    // broad announcement is opt-outable.
    Task<IReadOnlyList<GuestNotificationResponse>> BroadcastToAllGuestsAsync(NotificationContent content, CancellationToken ct = default);
}
