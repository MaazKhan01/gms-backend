using System;
using System.Collections.Generic;
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

    /// <summary>How long a vehicle counts as busy from its pickup when the ride
    /// has no planned drop-off. Admin bookings now require one, so this only
    /// covers rows created before that rule and guest on-demand requests (the
    /// guest doesn't know when they'll be dropped off). Tune it here.</summary>
    TimeSpan DefaultRideDuration { get; }
}

// Single source of truth for the double-booking rules, used by admin-scheduled
// transportation, the guest travel form and guest on-demand ride requests.
//
// Guest and driver are point-in-time checks (Date + Hour + Minute; seconds are
// ignored). The vehicle check is an interval check instead — a car booked
// 10:00→10:45 must also block 10:30, which an equality test would wave through.
public interface ITransportationConflictValidator
{
    /// <summary>Is this event participation free at <paramref name="candidateTime"/>?
    /// Scoped to the EventGuest, not the person: the same human on two events the
    /// same day keeps two independent schedules.</summary>
    Task<ConflictCheckResult> CheckGuestConflictAsync(
        int eventGuestId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default);

    Task<ConflictCheckResult> CheckDriverConflictAsync(
        int driverId, DateTime candidateTime, int? excludeTransportId = null, CancellationToken ct = default);

    /// <summary>Is this vehicle free for [start, end)? `end` may be null for a
    /// ride with no planned drop-off — <see cref="IConflictWindowPolicy.DefaultRideDuration"/>
    /// stands in. Pass the transport being edited as excludeTransportId so it
    /// doesn't clash with itself.</summary>
    Task<ConflictCheckResult> CheckVehicleConflictAsync(
        int vehicleId, DateTime start, DateTime? end, int? excludeTransportId = null, CancellationToken ct = default);

    /// <summary>Internal ids of every vehicle already booked over [start, end) —
    /// what the "available vehicles" dropdown feed subtracts.</summary>
    Task<List<int>> GetBusyVehicleIdsAsync(
        DateTime start, DateTime? end, int? excludeTransportId = null, CancellationToken ct = default);

    /// <summary>DriverProfile ids of every driver already assigned a ride over
    /// [start, end) — what the "available drivers" dropdown feed subtracts. This
    /// is the interval rule (same as vehicles), so it hides more than the
    /// point-in-time <see cref="CheckDriverConflictAsync"/> would reject on save.</summary>
    Task<List<int>> GetBusyDriverIdsAsync(
        DateTime start, DateTime? end, int? excludeTransportId = null, CancellationToken ct = default);
}
