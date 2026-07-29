using System;

namespace DomainPersistence.Entities;

/// <summary>A guest's on-demand ride request, before a driver accepts it. Once
/// accepted, a <see cref="Transport"/> row is created (RideRequestId set on it)
/// and the driver-facing execution lifecycle (arrived/in-progress/completed)
/// runs entirely through the existing Transport/TripStatus machinery.</summary>
public class RideRequest : Entity
{
    public int GuestId { get; set; }
    public int? PickupLocationId { get; set; }
    public int? DropoffLocationId { get; set; }
    public DateTime? RequestedTime { get; set; }
    public string Notes { get; set; }
    public string Status { get; set; }             // Open | Accepted | Cancelled
    public int? AcceptedByDriverId { get; set; }
    public DateTime? AcceptedAt { get; set; }

    // EF optimistic-concurrency token — the "first driver wins" accept race is
    // resolved by SQL Server's atomic UPDATE ... WHERE RowVersion = @original.
    public byte[] RowVersion { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual Location PickupLocation { get; set; }
    public virtual Location DropoffLocation { get; set; }
    public virtual DriverProfile AcceptedByDriver { get; set; }
    // Set once this request is accepted — the Transport row driving execution.
    public virtual Transport Transport { get; set; }
}
