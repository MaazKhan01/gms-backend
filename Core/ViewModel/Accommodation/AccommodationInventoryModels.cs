using System;
using System.Collections.Generic;

namespace Core.ViewModel.Accommodation;

// ── Contracts: which hotels this event may use ────────────────────────────────

public class CreateHotelContractRequest
{
    /// <summary>Public Guid of the AccommodationHotel (GET /v1/lookups/hotels).</summary>
    public Guid HotelId { get; set; }
    public string Notes { get; set; }
}

/// <summary>Edit carries no HotelId: moving a contract to a different hotel would
/// silently reassign every room block under it. Delete and re-add instead.</summary>
public class UpdateHotelContractRequest
{
    public string Notes { get; set; }
}

public class HotelContractResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid HotelId { get; set; }
    public string HotelName { get; set; }
    public string HotelAddress { get; set; }
    public string HotelImageUrl { get; set; }
    public string Notes { get; set; }
    /// <summary>How many room blocks hang off this contract.</summary>
    public int InventoryCount { get; set; }
    /// <summary>Sum of RoomCount across those blocks — a headline number, NOT a
    /// per-night capacity (overlapping windows are counted once each).</summary>
    public int TotalRooms { get; set; }
}

// ── Inventory: room blocks held under a contract ──────────────────────────────

public class CreateRoomInventoryRequest
{
    /// <summary>Public Guid of the EventHotelContract this block belongs to.</summary>
    public Guid ContractId { get; set; }
    /// <summary>Public Guid of the AccommodationRoomType.</summary>
    public Guid RoomTypeId { get; set; }
    public int RoomCount { get; set; }
    /// <summary>First night held (inclusive).</summary>
    public DateOnly? FromDate { get; set; }
    /// <summary>Last night held (inclusive).</summary>
    public DateOnly? ToDate { get; set; }
    public string Notes { get; set; }
}

/// <summary>Same fields as create, minus ContractId — a block never moves hotel.</summary>
public class UpdateRoomInventoryRequest
{
    public Guid RoomTypeId { get; set; }
    public int RoomCount { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string Notes { get; set; }
}

public class RoomInventoryResponse
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid HotelId { get; set; }
    public string HotelName { get; set; }
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; }
    public int RoomCount { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int Nights { get; set; }
    public string Notes { get; set; }
}

// ── Availability ──────────────────────────────────────────────────────────────

/// <summary>One night's picture for a hotel + room type: how many rooms are held,
/// how many are taken, how many are left.</summary>
public class RoomAvailabilityNight
{
    public DateOnly Date { get; set; }
    public int Total { get; set; }
    public int Booked { get; set; }
    public int Available { get; set; }
}

/// <summary>One hotel + room type's nights. A response carries one series per
/// pair the event holds rooms for, so the same call answers both "is this room
/// free" (filter to one pair) and the whole inventory grid (filter to none).</summary>
public class RoomAvailabilitySeries
{
    public Guid HotelId { get; set; }
    public string HotelName { get; set; }
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; }
    public List<RoomAvailabilityNight> Nights { get; set; } = new();
}

public class RoomAvailabilityResponse
{
    /// <summary>Window every series spans — the earliest and latest night the
    /// event holds any room for. Null when it holds none.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Empty means nothing is held for the requested scope, so nothing is
    /// enforced there either — an unmanaged hotel is not a full one.
    ///
    /// No per-night totals row here on purpose: the grid filters by hotel on the
    /// client, so a server-side total would be the wrong subtotal the moment a
    /// filter is on. Summing the visible series is the only correct answer.</summary>
    public List<RoomAvailabilitySeries> Series { get; set; } = new();
}
