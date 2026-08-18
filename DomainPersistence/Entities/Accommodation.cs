namespace DomainPersistence.Entities;

/// <summary>A guest's hotel stay.
///
/// The hotel is reached through <see cref="EventHotelContract"/>, never picked
/// directly: a stay may only sit at a hotel this event has a contract with.
/// <see cref="EventId"/> is carried on the row so both foreign keys can be
/// composite — the participation and the contract are then forced to belong to
/// the SAME event by the database, not by whoever remembered to check. It is
/// always derived server-side from the resolved EventGuest.</summary>
public class Accommodation : Entity
{
    public int EventGuestId { get; set; }

    /// <summary>Denormalised from EventGuest.EventId to close both composite FKs.
    /// Never taken from a client payload.</summary>
    public int EventId { get; set; }

    public int EventHotelContractId { get; set; }

    public int? RoomTypeId { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
    public string ImageUrl { get; set; }
    public virtual EventGuest EventGuest { get; set; }
    public virtual EventHotelContract Contract { get; set; }
    public virtual AccommodationRoomType RoomType { get; set; }
}
