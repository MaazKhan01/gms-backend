namespace DomainPersistence.Entities;

/// <summary>Ground-transport vehicle category — Sedan / SUV / Van / ... —
/// dedicated table, matching the FlightType/FlightClass pattern.</summary>
public class VehicleType : Entity
{
    public string Name { get; set; }
}
