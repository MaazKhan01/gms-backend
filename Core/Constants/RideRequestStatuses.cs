namespace Core.Constants;

/// <summary>Lifecycle of a guest on-demand ride request (RideRequests.Status),
/// before a driver accepts it. Once Accepted, a Transport row takes over the
/// execution lifecycle via the existing TransportStatuses machine.</summary>
public static class RideRequestStatuses
{
    public const string Open = "Open";
    public const string Accepted = "Accepted";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Open, Accepted, Cancelled };

    /// <summary>Statuses that count as "still open" for conflict-checking.</summary>
    public static readonly string[] Active = { Open, Accepted };
}
