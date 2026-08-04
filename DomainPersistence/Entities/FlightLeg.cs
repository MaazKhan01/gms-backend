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
    // A return booking's two legs can be on different fare classes/seats
    // (e.g. Business outbound, Economy inbound) — Flight.FlightClassId/Seat
    // stay as a "primary" copy (mirrored from the first leg) for anywhere
    // that only cares about one value, e.g. a collapsed list row.
    public int? FlightClassId { get; set; }
    public string Seat { get; set; }

    public virtual Flight Flight { get; set; }
    public virtual AirportData FromAirport { get; set; }
    public virtual AirportData ToAirport { get; set; }
    public virtual FlightClass FlightClass { get; set; }
}
