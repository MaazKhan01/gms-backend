using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Accommodation;
using Core.ViewModel.Common;
using Core.ViewModel.Travel;

namespace Core.Interfaces.Services;

/// <summary>Per-event hotel contracts and the room blocks held under them, plus
/// the availability arithmetic those blocks exist for. Hotels and room types stay
/// global lookups; this is what scopes them to an event and caps how many rooms
/// may be booked on any one night.</summary>
public interface IAccommodationInventoryService
{
    // ── Contracts ────────────────────────────────────────────────────────────
    Task<ApiResponse<List<HotelContractResponse>>> GetContractsAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<HotelContractResponse>> CreateContractAsync(Guid eventId, CreateHotelContractRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<HotelContractResponse>> UpdateContractAsync(Guid eventId, Guid contractId, UpdateHotelContractRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteContractAsync(Guid eventId, Guid contractId, int userId, CancellationToken ct = default);

    // ── Inventory ────────────────────────────────────────────────────────────
    /// <summary>Room blocks for the event, optionally narrowed to one hotel.</summary>
    Task<ApiResponse<List<RoomInventoryResponse>>> GetInventoryAsync(Guid eventId, Guid? hotelId = null, CancellationToken ct = default);
    Task<ApiResponse<RoomInventoryResponse>> CreateInventoryAsync(Guid eventId, CreateRoomInventoryRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<RoomInventoryResponse>> UpdateInventoryAsync(Guid eventId, Guid inventoryId, UpdateRoomInventoryRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteInventoryAsync(Guid eventId, Guid inventoryId, int userId, CancellationToken ct = default);

    /// <summary>Change the rooms held on ONE night of a block, leaving every other
    /// night of it as it was. The block is split around that night — the row keeps
    /// its identity as the single-night piece and the untouched ends become new
    /// rows. This is what the availability grid's editable cells call.</summary>
    Task<ApiResponse<bool>> SetNightRoomCountAsync(
        Guid eventId, Guid inventoryId, SetNightRoomCountRequest request, int userId, CancellationToken ct = default);

    // ── Booking-form feeds ───────────────────────────────────────────────────
    /// <summary>Hotels this event has a contract with — what the accommodation
    /// form's hotel dropdown should show instead of every hotel in the system.</summary>
    Task<ApiResponse<List<HotelDto>>> GetContractedHotelsAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Room types that actually have rooms held at this hotel for this
    /// event. Empty means the hotel is unmanaged — the caller should fall back to
    /// the global room-type lookup.</summary>
    Task<ApiResponse<List<IdNameDto>>> GetContractedRoomTypesAsync(Guid eventId, Guid hotelId, CancellationToken ct = default);

    /// <summary>Per-night held / booked / left, one series per hotel + room type
    /// the event holds rooms for, plus per-night totals across them. Filter to a
    /// single pair for the booking form's blocked dates and "rooms left" hint;
    /// leave both null for the whole inventory grid.</summary>
    Task<ApiResponse<RoomAvailabilityResponse>> GetAvailabilityAsync(
        Guid eventId, Guid? hotelId = null, Guid? roomTypeId = null, CancellationToken ct = default);

    // ── Enforcement (called by the accommodation save path) ──────────────────
    /// <summary>Null when the stay fits, otherwise the message to reject it with.
    /// Ids are internal. Pass the accommodation being edited as
    /// excludeAccommodationId so it doesn't count against itself.</summary>
    Task<string> CheckStayAvailabilityAsync(
        int eventId, int hotelId, int? roomTypeId, DateOnly checkIn, DateOnly checkOut,
        int? excludeAccommodationId = null, CancellationToken ct = default);
}
