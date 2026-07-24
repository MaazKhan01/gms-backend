namespace DomainPersistence.Entities;

/// <summary>Direction of a flight booking. Seeded: Inbound / Outbound / Return.</summary>
public class FlightType : Entity
{
    public string Name { get; set; }
}
