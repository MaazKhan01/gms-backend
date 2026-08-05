using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>An event's contract with one hotel: the agreement that this hotel
/// may house this event's guests at all. Hotels themselves stay global
/// (<see cref="AccommodationHotel"/> is a lookup) — the contract is what makes
/// one available to an event, and what the room blocks hang off.</summary>
public class EventHotelContract : Entity
{
    public int EventId { get; set; }

    public int AccommodationHotelId { get; set; }

    public string Notes { get; set; }

    public virtual Event Event { get; set; }

    public virtual AccommodationHotel Hotel { get; set; }

    public virtual ICollection<HotelRoomInventory> Inventory { get; set; } = new List<HotelRoomInventory>();
}
