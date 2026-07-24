namespace DomainPersistence.Entities;

/// <summary>A flight booking for a guest. Segments live in <see cref="FlightLeg"/>.</summary>
public class Flight : Entity
{
    public int GuestId { get; set; }
    public int FlightTypeId { get; set; }
    public int? FlightClassId { get; set; }
    public string Status { get; set; }   // Confirmed / Pending
    public string Seat { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual FlightType FlightType { get; set; }
    public virtual FlightClass FlightClass { get; set; }
    public virtual ICollection<FlightLeg> Legs { get; set; } = new List<FlightLeg>();
}
