namespace DomainPersistence.Entities;

/// <summary>A guest's hotel stay.</summary>
public class Accommodation : Entity
{
    public int GuestId { get; set; }
    public int AccommodationHotelId { get; set; }
    public int? RoomTypeId { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }

    /// <summary>Optional image for this stay — a booking voucher, or the room
    /// itself. A blob URL from POST /api/v1/upload, stored without its SAS token
    /// (BlobSasMiddleware re-signs it on every read). Distinct from
    /// AccommodationHotel.ImageUrl, which is the hotel's own picture.</summary>
    public string ImageUrl { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual AccommodationHotel Hotel { get; set; }
    public virtual AccommodationRoomType RoomType { get; set; }
}
