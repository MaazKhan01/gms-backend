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
// external push SDK. Every recipient (staff, driver, or guest — all Users
// now) is reached via the built-in Clients.User(...), keyed by their own
// Users.Id (their NameIdentifier claim — see VipAppService.BuildAccessToken
// for why a guest token's NameIdentifier is the guest's linked User.Id, not
// Guest.Id, precisely so this works uniformly for every audience).
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

            await _realTimeAlertService.SendToUserAsync(
                payload.Topic, payload.UserId.ToString(), payload.Title, payload.Body, data
            ).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            // Best-effort — the durable Notification row already persisted by
            // the caller is the source of truth, this is just the real-time nudge.
            _logger.LogError(ex, "Error dispatching manual push notification");
        }
    }
}
