using Core.Authorization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants.Notification;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Noification;
using Core.ViewModel.VipApp;
using Microsoft.EntityFrameworkCore;

// Matches the existing convention of this folder — no namespace declared.
//
// Template-code overloads over INotificationManagerService: the fan-out engine
// stays generic (it only knows recipients + NotificationContent), the wording
// stays in NotificationTemplates, and a calling service writes one line:
//
//   await _notifications.SendToDriverAsync(_unitOfWork, driverProfileId,
//       NotificationTemplates.TransportDriverAssigned,
//       new Dictionary<string, string> { ["guestName"] = name });
//
// Extensions rather than new interface methods so INotificationManagerService
// (and its DI registration) needs no change as more notifications land.
public static class NotificationSendExtensions
{
    public static Task<NotificationResponse> SendToUserAsync(
        this INotificationManagerService notifications,
        int userId, string templateCode, IDictionary<string, string> tokens = null, CancellationToken ct = default)
        => notifications.SendToUserAsync(userId, NotificationTemplates.Build(templateCode, tokens), ct);

    public static Task<IReadOnlyList<NotificationResponse>> SendToUsersAsync(
        this INotificationManagerService notifications,
        IEnumerable<int> userIds, string templateCode, IDictionary<string, string> tokens = null, CancellationToken ct = default)
        => notifications.SendToUsersAsync(userIds, NotificationTemplates.Build(templateCode, tokens), ct);

    public static Task<IReadOnlyList<NotificationResponse>> SendToRoleAsync(
        this INotificationManagerService notifications,
        string roleCode, string templateCode, IDictionary<string, string> tokens = null, CancellationToken ct = default)
        => notifications.SendToRoleAsync(roleCode, NotificationTemplates.Build(templateCode, tokens), ct);

    public static Task<IReadOnlyList<NotificationResponse>> SendToPermissionAsync(
        this INotificationManagerService notifications,
        string permissionCode, AccessLevel level, string templateCode,
        IDictionary<string, string> tokens = null, CancellationToken ct = default)
        => notifications.SendToPermissionAsync(permissionCode, level, NotificationTemplates.Build(templateCode, tokens), ct);

    public static Task<GuestNotificationResponse> SendToGuestAsync(
        this INotificationManagerService notifications,
        int guestId, string templateCode, IDictionary<string, string> tokens = null, CancellationToken ct = default)
        => notifications.SendToGuestAsync(guestId, NotificationTemplates.Build(templateCode, tokens), ct);

    // Drivers are addressed by DriverProfile.Id everywhere in the transportation
    // module, but notifications target Users — this is the one place that hop
    // lives. Returns null when the profile has no user row (never throws: a
    // missing notification must not fail the operation that triggered it).
    public static async Task<NotificationResponse> SendToDriverAsync(
        this INotificationManagerService notifications,
        IUnitOfWork unitOfWork, int driverProfileId, string templateCode,
        IDictionary<string, string> tokens = null, CancellationToken ct = default)
    {
        var driverUserId = await unitOfWork.DriverProfiles.Query()
            .Where(d => d.Id == driverProfileId)
            .Select(d => d.UserId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return driverUserId == 0
            ? null
            : await notifications.SendToUserAsync(driverUserId, templateCode, tokens, ct).ConfigureAwait(false);
    }
}
