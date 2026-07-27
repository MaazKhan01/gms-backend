using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Noification;

// Matches the existing convention of this folder — no namespace declared.
//
// Real device push (FCM/APNs), for when the recipient's app isn't open — see
// ManualNotificationProvider for the live in-app SignalR channel, which this
// runs alongside (not instead of): both are registered as IPushNotificationProvider
// in ServiceExtensions and NotificationManagerService fans out to every one.
//
// The multi-device fan-out here IS real: a guest can be logged into several
// devices (DomainPersistence.Entities.GuestDevice — one row per device/token,
// upserted by RegisterGuestDeviceAsync), and every one of them gets its own
// send attempt, independently. What's still a stub is the actual wire call —
// SendToDeviceAsync — pending a real Firebase project:
//   1. Add the FirebaseAdmin NuGet package.
//   2. Initialize FirebaseApp.Create(...) from a service-account JSON (config-driven).
//   3. Replace SendToDeviceAsync's body with a real
//      FirebaseMessaging.DefaultInstance.SendAsync(...) call.
//   4. When FCM reports a token as invalid/unregistered, delete that
//      GuestDevice row here (a dead token should stop being retried).
//
// Users (admin/staff) have no device-registration table today — there's no
// mobile app for them, so User recipients are a deliberate no-op below.
public class FirebaseNotificationProvider(
    IUnitOfWork _unitOfWork,
    IConfiguration _configuration,
    ILogger<FirebaseNotificationProvider> _logger) : IPushNotificationProvider
{
    public async Task SendAsync(PushNotificationPayload payload, CancellationToken ct = default)
    {
        if (payload.RecipientType != NotificationRecipientType.Guest || !payload.GuestId.HasValue)
            return; // Users: no device table yet — nothing to push to.

        var devices = await _unitOfWork.GuestDevices.Query()
            .Where(d => d.GuestId == payload.GuestId.Value)
            .ToListAsync(ct).ConfigureAwait(false);

        if (devices.Count == 0) return;

        foreach (var device in devices)
        {
            try
            {
                await SendToDeviceAsync(device.Token, device.Platform, payload, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // One dead/invalid token must not stop the guest's other devices.
                _logger.LogError(ex, "Error pushing to device {DeviceId} (guest {GuestId})", device.Id, payload.GuestId);
            }
        }
    }

    // Stub — see the class remarks above for the real FCM/APNs integration steps.
    private Task SendToDeviceAsync(string token, string platform, PushNotificationPayload payload, CancellationToken ct)
    {
        _logger.LogInformation(
            "[stub] would push to {Platform} device (token ending '{TokenSuffix}'): {Title}",
            platform, token.Length > 6 ? token[^6..] : token, payload.Title);
        return Task.CompletedTask;
    }
}
