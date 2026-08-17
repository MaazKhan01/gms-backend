using System.Collections.Generic;
using DomainPersistence.Enums;

namespace DomainPersistence.Entities;

/// <summary>A concrete ground-transport vehicle in the fleet — a specific car,
/// as opposed to <see cref="VehicleType"/> which is only its category.</summary>
public class Vehicle : Entity
{
    public int VehicleTypeId { get; set; }

    // Fixed = dedicated to a single Open driver; Open = shared pool car Fixed
    // drivers draw from per trip. Nullable because vehicles created before this
    // column existed have no value — same convention as DriverProfile.DriverType.
    public VehicleUsageType? UsageType { get; set; }

    // Optional: existing rows predate fleet providers, and an in-house car has none.
    public int? FleetProviderId { get; set; }

    public string VehicleModel { get; set; }

    public string VehicleNumber { get; set; }

    public string VehicleImage { get; set; }

    public int? Capacity { get; set; }

    public virtual VehicleType VehicleType { get; set; }

    public virtual FleetProvider FleetProvider { get; set; }

    // Drivers holding this car permanently. A collection rather than a single
    // reference because an Open vehicle is shareable; a Fixed one is capped at a
    // single row by a unique index on DriverProfiles.AssignedVehicleId.
    public virtual ICollection<DriverProfile> DriverAssignments { get; set; } = new List<DriverProfile>();
}
