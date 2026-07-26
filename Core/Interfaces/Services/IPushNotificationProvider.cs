using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Noification;

namespace Core.Interfaces.Services;

// Notification provider abstraction — swap the DI registration in
// ServiceExtensions to move from ManualNotificationProvider (today, SignalR-only)
// to FirebaseNotificationProvider (future, real push) with no caller changes.
public interface IPushNotificationProvider
{
    Task SendAsync(PushNotificationPayload payload, CancellationToken ct = default);
}
