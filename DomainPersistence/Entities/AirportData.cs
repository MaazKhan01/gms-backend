namespace DomainPersistence.Entities;

/// <summary>Airport lookup — referenced by <see cref="FlightLeg"/> from/to.</summary>
public class AirportData : Entity
{
    public string Code { get; set; }          // IATA code, e.g. DXB
    public string City { get; set; }
    public string Country { get; set; }
    public string Continent { get; set; }
    public int? LocationId { get; set; }

    public virtual Location Location { get; set; }
}
