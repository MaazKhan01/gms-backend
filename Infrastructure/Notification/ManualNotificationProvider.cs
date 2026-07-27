using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Noification;
using Microsoft.Extensions.Logging;

// Matches the existing convention of this folder (NotificationManagerService,
// RealTimeAlertService) — no namespace declared, global namespace.
//
// Current implementation: delivers over the existing SignalR hub only — no
// external push SDK. A User recipient is reached via the built-in
// Clients.User(...) (their NameIdentifier claim). A Guest recipient is reached
// via a dedicated "guest:{id}" group (see RealTimeHubService) rather than
// Clients.User(...), because guest and user JWTs can carry the same numeric id
// and would otherwise collide on the SignalR user-id concept.
public class ManualNotificationProvider(
    IRealTimeAlertService _realTimeAlertService,
    ILogger<ManualNotificationProvider> _logger) : IPushNotificationProvider
{
    public async Task SendAsync(PushNotificationPayload payload, CancellationToken ct = default)
    {
        try
        {
            // Prefer the rich payload (full response DTO) when present so the
            // client can render without a follow-up fetch; fall back to the
            // string-only Data dictionary.
            object data = payload.Payload ?? payload.Data;

            if (payload.RecipientType == NotificationRecipientType.User && payload.UserId.HasValue)
            {
                await _realTimeAlertService.SendToUserAsync(
                    payload.Topic, payload.UserId.Value.ToString(), payload.Title, payload.Body, data
                ).ConfigureAwait(false);
            }
            else if (payload.RecipientType == NotificationRecipientType.Guest && payload.GuestId.HasValue)
            {
                await _realTimeAlertService.SendToGroupAsync(
                    payload.Topic, $"guest:{payload.GuestId.Value}", payload.Title, payload.Body, data
                ).ConfigureAwait(false);
            }
        }
        catch (System.Exception ex)
        {
            // Best-effort — the durable Notification/GuestNotification row already
            // persisted by the caller is the source of truth, this is just the
            // real-time nudge.
            _logger.LogError(ex, "Error dispatching manual push notification");
        }
    }
}
