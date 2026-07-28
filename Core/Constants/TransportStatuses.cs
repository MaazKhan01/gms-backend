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
    public const string Completed = "completed";

    /// <summary>The status a driver may move a job to, given its current one.</summary>
    public static string NextFor(string current) => current switch
    {
        Assigned => Arrived,
        Arrived => InProgress,
        InProgress => Completed,
        _ => null,
    };
}
