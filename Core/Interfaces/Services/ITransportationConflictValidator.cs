using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

public class ConflictCheckResult
{
    public bool HasConflict { get; set; }
    public string Message { get; set; }

    public static ConflictCheckResult Ok() => new() { HasConflict = false };
    public static ConflictCheckResult Conflict(string message) => new() { HasConflict = true, Message = message };
}

// Today's policy is zero-buffer (exact minute match only). Injecting this
// lets a future buffer window (e.g. "no rides within 15 minutes of another")
// be added by swapping the DI registration — the validator's call sites and
// comparison logic don't change.
public interface IConflictWindowPolicy
{
    TimeSpan BufferBefore { get; }
    TimeSpan BufferAfter { get; }
}

// Single source of truth for the guest/driver double-booking rule, used by
// both admin-scheduled transportation and guest on-demand ride requests.
// Compares Date + Hour + Minute only — seconds/milliseconds are ignored.
public interface ITransportationConflictValidator
{
    Task<ConflictCheckResult> CheckGuestConflictAsync(
        int guestId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default);

    Task<ConflictCheckResult> CheckDriverConflictAsync(
        int driverId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default);
}
