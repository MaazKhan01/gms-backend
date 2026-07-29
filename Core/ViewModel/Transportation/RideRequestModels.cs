using System;

namespace Core.ViewModel.Transportation;

// ── Guest on-demand ride requests (Day 2) ────────────────────────────────────
public class CreateRideRequestRequest
{
    public Guid? PickupLocationId { get; set; }
    public Guid? DropoffLocationId { get; set; }
    public DateTime? RequestedTime { get; set; }
    public string Notes { get; set; }
}

public class RideRequestRow
{
    public Guid Id { get; set; }
    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }
    public DateTime? RequestedTime { get; set; }
    public string Status { get; set; }
    public Guid? AcceptedByDriverId { get; set; }
    public string AcceptedByDriverName { get; set; }
    public DateTime? AcceptedAt { get; set; }
    // Set only once a driver has accepted — the Transport row now driving execution.
    public Guid? TransportId { get; set; }
}
