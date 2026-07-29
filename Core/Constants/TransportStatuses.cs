namespace Core.Constants;

/// <summary>
/// Lifecycle of a ground transfer (Transports.TripStatus). The admin side only
/// ever produces Pending / Assigned — the rest is driven by the driver app
/// (TransportAppController):
///
///   pending  →  assigned  →  arrived  →  in-progress  →  completed
///   (no driver) (driver set) (at pickup) (guest on board) (dropped off)
/// </summary>
public static class TransportStatuses
{
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
    public static readonly string[] All = { Pending, Assigned, Arrived, InProgress, Completed, Cancelled };

    /// <summary>Statuses that count as "still open" for conflict-checking — a
    /// cancelled ride no longer occupies its time slot.</summary>
    public static readonly string[] Active = { Pending, Assigned, Arrived, InProgress, Completed };

    /// <summary>The status a driver may move a job to, given its current one.</summary>
    public static string NextFor(string current) => current switch
    {
        Assigned => Arrived,
        Arrived => InProgress,
        InProgress => Completed,
        _ => null,
    };
}
