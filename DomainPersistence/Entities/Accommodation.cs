namespace DomainPersistence.Entities;

/// <summary>A guest's hotel stay.</summary>
public class Accommodation : Entity
{
    public int GuestId { get; set; }
    public int AccommodationHotelId { get; set; }
    public int? RoomTypeId { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual AccommodationHotel Hotel { get; set; }
    public virtual AccommodationRoomType RoomType { get; set; }
}
