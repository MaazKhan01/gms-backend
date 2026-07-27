namespace DomainPersistence.Entities;

/// <summary>Airport lookup — referenced by <see cref="FlightLeg"/> from/to.</summary>
public class AirportData : Entity
{
    public string Code { get; set; }          // IATA code, e.g. DXB
    public string AirportName { get; set; }
    public int? LocationId { get; set; }

    public virtual Location Location { get; set; }
}
