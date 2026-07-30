using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;

// Matches the existing convention of this folder — no namespace declared.
//
// Hard-deletes rows via ExecuteDeleteAsync (a direct "DELETE FROM ... WHERE",
// bypassing change tracking and AuditInterceptor's soft-delete conversion) —
// this job IS the intentional permanent purge, not a user-facing delete.
// IgnoreQueryFilters() is required so already soft-deleted rows are included
// in the sweep too, since the global query filter would otherwise hide them.
public class NotificationCleanupJob(
    IUnitOfWork _unitOfWork,
    IConfiguration _configuration,
    ILogger<NotificationCleanupJob> _logger) : INotificationCleanupJob
{
    public async Task PurgeOldNotificationsAsync(CancellationToken ct = default)
    {
        var retentionDays = _configuration.GetValue<int?>("Notifications:RetentionDays") ?? 90;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        try
        {
            // Guests are Users now — one Notifications table for every audience,
            // so one purge sweep covers all of them.
            var notificationsDeleted = await _unitOfWork.Notifications.Query()
                .IgnoreQueryFilters()
                .Where(n => n.CreatedAt < cutoff && (n.Read == true || n.IsDeleted == true))
                .ExecuteDeleteAsync(ct);

            _logger.LogInformation(
                "Notification cleanup: purged {Count} notification(s) older than {Days} day(s)",
                notificationsDeleted, retentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging old notifications");
            throw; // let Hangfire record + retry the failure
        }
    }
}
