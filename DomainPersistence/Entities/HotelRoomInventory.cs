using System;

namespace DomainPersistence.Entities;

/// <summary>A block of rooms held under one <see cref="EventHotelContract"/>:
/// "12 Deluxe rooms, 5–9 August".
///
/// Dates are INCLUSIVE NIGHTS — FromDate is the first night held, ToDate the
/// last. A stay of CheckIn 5 → CheckOut 7 occupies nights 5 and 6, so this block
/// covers it. (Deliberately not check-in/check-out semantics: an admin typing a
/// room block thinks in nights held, and an exclusive end date reads as one
/// night more than they meant.)
///
/// Several blocks may exist for the same room type. Where their windows overlap
/// the counts ADD UP — that's how "10 rooms all week, 5 more over the weekend"
/// is expressed, and it means no unique constraint on the window.</summary>
public class HotelRoomInventory : Entity
{
    public int EventHotelContractId { get; set; }

    public int RoomTypeId { get; set; }

    /// <summary>How many rooms of this type are held over the window.</summary>
    public int RoomCount { get; set; }

    /// <summary>First night held (inclusive).</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>Last night held (inclusive).</summary>
    public DateOnly ToDate { get; set; }

    public string Notes { get; set; }

    public virtual EventHotelContract Contract { get; set; }

    public virtual AccommodationRoomType RoomType { get; set; }
}
