using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Services;
using Core.ViewModel.Noification;
using Microsoft.Extensions.Configuration;

// Matches the existing convention of this folder — no namespace declared.
//
// Future implementation — NOT registered in DI yet (ManualNotificationProvider is
// the active IPushNotificationProvider; see ServiceExtensions). Swap-in path once
// real push is needed:
//   1. Add the FirebaseAdmin NuGet package.
//   2. Initialize FirebaseApp.Create(...) from a service-account JSON (config-driven).
//   3. Resolve the target device token(s): DomainPersistence.Entities.GuestDevice
//      already stores one FCM/APNs token per guest device — no schema change needed.
//   4. Call FirebaseMessaging.DefaultInstance.SendAsync(message, ct).
//   5. Flip the registration in ServiceExtensions:
//      services.AddScoped<IPushNotificationProvider, FirebaseNotificationProvider>();
public class FirebaseNotificationProvider(IConfiguration _configuration) : IPushNotificationProvider
{
    public Task SendAsync(PushNotificationPayload payload, CancellationToken ct = default)
    {
        throw new System.NotImplementedException(
            "Firebase push is not wired up yet — register ManualNotificationProvider instead. See class remarks for the integration steps.");
    }
}
