namespace DomainPersistence.Entities;

/// <summary>A guest's ground transport.</summary>
public class Transport : Entity
{
    public int GuestId { get; set; }
    public int? PickupLocationId { get; set; }
    public int? DropoffLocationId { get; set; }
    public int? VehicleId { get; set; }       // -> Vehicles (the assigned car, not its category)
    public int? DriverId { get; set; }        // -> DriverProfiles
    public string TripStatus { get; set; }    // On Time / Delayed
    // Planned times, then what actually happened (filled in by dispatch).
    public DateTime? PickupTime { get; set; }
    public DateTime? DropoffTime { get; set; }
    public DateTime? ActualPickupTime { get; set; }
    public DateTime? ActualDropOffTime { get; set; }

    public string Notes { get; set; }

    // "scheduled" (admin-created) | "on-demand" (guest-requested, see RideRequest).
    public string RideSource { get; set; }
    // Set only when this ride originated from an accepted on-demand RideRequest.
    public int? RideRequestId { get; set; }

    // Future pricing — nullable, no payment integration yet.
    public decimal? BaseFare { get; set; }
    public decimal? DistanceFare { get; set; }
    public decimal? WaitingFare { get; set; }
    public decimal? TotalFare { get; set; }
    public string Currency { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual Location PickupLocation { get; set; }
    public virtual Location DropoffLocation { get; set; }
    public virtual Vehicle Vehicle { get; set; }
    public virtual DriverProfile Driver { get; set; }
    public virtual RideRequest RideRequest { get; set; }
}
