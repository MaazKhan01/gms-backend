namespace DomainPersistence.Entities;

/// <summary>One segment of a <see cref="Flight"/>.</summary>
public class FlightLeg : Entity
{
    public int FlightId { get; set; }
    public string FlightNumber { get; set; }
    public string DepartureCode { get; set; }   // airport code
    public string DepartureCity { get; set; }
    public string ArrivalCode { get; set; }
    public string ArrivalCity { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public virtual Flight Flight { get; set; }
}
