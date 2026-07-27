namespace DomainPersistence.Entities;

/// <summary>A guest's ground transport.</summary>
public class Transport : Entity
{
    public int GuestId { get; set; }
    public int? PickupLocationId { get; set; }
    public int? DropoffLocationId { get; set; }
    public int? VehicleTypeId { get; set; }
    public int? DriverId { get; set; }        // -> DriverProfiles
    public string TripStatus { get; set; }    // On Time / Delayed
    public DateTime? PickupTime { get; set; }
    public DateTime? EstimatedArrival { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual Location PickupLocation { get; set; }
    public virtual Location DropoffLocation { get; set; }
    public virtual VehicleType VehicleType { get; set; }
    public virtual DriverProfile Driver { get; set; }
}
