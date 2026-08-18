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

    /// <summary>
    /// Notifies a PERSON. <paramref name="guestId"/> is a <c>Guest.Id</c> — the
    /// internal id of the master person record, which resolves to their linked
    /// User and that user's devices.
    /// </summary>
    /// <remarks>
    /// Never pass an <c>EventGuest.Id</c> here. Both are bare ints from the same
    /// identity sequence, so the wrong one does not fail — it silently notifies
    /// whichever unrelated person happens to hold that number. Callers holding a
    /// participation (transport, seating, travel) must resolve
    /// <c>EventGuest.GuestId</c> first.
    /// </remarks>
    Task<GuestNotificationResponse> SendToGuestAsync(int guestId, NotificationContent content, CancellationToken ct = default);

    /// <summary>Same contract as <see cref="SendToGuestAsync"/>: these are
    /// <c>Guest.Id</c> values, never EventGuest ids.</summary>
    Task<IReadOnlyList<GuestNotificationResponse>> SendToGuestsAsync(IEnumerable<int> guestIds, NotificationContent content, CancellationToken ct = default);

    // Every Guest with NotificationsEnabled (their own in-app preference toggle —
    // see Guest.NotificationsEnabled / VipAppService.UpdateSettingsAsync). Direct
    // SendToGuest(s)Async targeting ignores this flag deliberately: an admin who
    // picked specific guests (or a 1:1 support reply) should still land, only a
    // broad announcement is opt-outable.
    Task<IReadOnlyList<GuestNotificationResponse>> BroadcastToAllGuestsAsync(NotificationContent content, CancellationToken ct = default);
}
