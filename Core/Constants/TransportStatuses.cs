namespace Core.Constants;

/// <summary>
/// Lifecycle of a ground transfer (Transports.TripStatus). The admin side only
/// ever produces Pending / Assigned — the rest is driven by the driver app, one
/// endpoint per step (TransportAppController):
///
///   pending  →  assigned  →  in-progress  →  arrived  →  in-transit  →  completed
///   (no driver) (driver set) (start-job)    (arrived)   (start-trip)   (complete)
///                            (en route)     (at pickup) (guest aboard) (dropped off)
/// </summary>
public static class TransportStatuses
{
    /// <summary>Guest-requested from the VIP app, no driver yet — any driver may
    /// claim it (see the driver app's accept endpoint).</summary>
    public const string New = "new";

    public const string Pending = "pending";
    public const string Assigned = "assigned";
    public const string Arrived = "arrived";
    public const string InProgress = "in-progress";
    public const string InTransit = "in-transit";
    public const string Completed = "completed";
    // Terminal, explicit-action only — never reachable via NextFor(), so the
    // driver app's existing "advance" button can't land on it by accident.
    public const string Cancelled = "cancelled";

    /// <summary>Every valid status — used to reject junk status filters.</summary>
    public static readonly string[] All = { New, Pending, Assigned, InProgress, Arrived, InTransit, Completed };

    /// <summary>Statuses where the driver is already busy on a job.</summary>
    public static readonly string[] Active = { InProgress, Arrived, InTransit };

    /// <summary>The status a driver may move a job to, given its current one.</summary>
    public static string NextFor(string current) => current switch
    {
        Assigned => InProgress,
        InProgress => Arrived,
        Arrived => InTransit,
        InTransit => Completed,
        _ => null,
    };
}
