using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

// Recurring Hangfire job (see ServiceExtensions/Program.cs) — hard-purges
// already-read (or already soft-deleted) notifications past the retention
// window. Keeps "notification history" from growing forever while still
// letting users browse recent history via NotificationsController.
public interface INotificationCleanupJob
{
    Task PurgeOldNotificationsAsync(CancellationToken ct = default);
}
