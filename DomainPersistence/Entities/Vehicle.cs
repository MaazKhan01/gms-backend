namespace DomainPersistence.Entities;

/// <summary>A concrete ground-transport vehicle in the fleet — a specific car,
/// as opposed to <see cref="VehicleType"/> which is only its category.</summary>
public class Vehicle : Entity
{
    public int VehicleTypeId { get; set; }

    public string VehicleModel { get; set; }

    public string VehicleNumber { get; set; }

    public string VehicleImage { get; set; }

    public int? Capacity { get; set; }

    public virtual VehicleType VehicleType { get; set; }
}
