using DomainPersistence.Enums;

namespace DomainPersistence.Entities;

/// <summary>A flight booking for a guest. Segments live in <see cref="FlightLeg"/>.</summary>
public class Flight : Entity
{
    public int EventGuestId { get; set; }
    // Inbound/Outbound are one leg; Return is two (outbound + inbound).
    public FlightType FlightType { get; set; }
    public int? FlightClassId { get; set; }
    public string Status { get; set; }   // Confirmed / Pending
    public string Seat { get; set; }
    // Booking-level times entered on the travel form. The legs carry their own
    // Start/EndTime; these are what the guest app shows as depart/land.
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }

    /// <summary>Optional scan of the ticket / boarding pass, as a blob URL from
    /// POST /api/v1/upload. Stored without its SAS token — BlobSasMiddleware
    /// re-signs it on every read.</summary>
    public string ImageUrl { get; set; }

    public virtual EventGuest EventGuest { get; set; }
    public virtual FlightClass FlightClass { get; set; }
    public virtual ICollection<FlightLeg> Legs { get; set; } = new List<FlightLeg>();
}
