namespace DomainPersistence.Entities;

/// <summary>One segment of a <see cref="Flight"/>.</summary>
public class FlightLeg : Entity
{
    public int FlightId { get; set; }
    public string FlightNumber { get; set; }
    public int? FromAirportId { get; set; }
    public int? ToAirportId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public virtual Flight Flight { get; set; }
    public virtual AirportData FromAirport { get; set; }
    public virtual AirportData ToAirport { get; set; }
}
