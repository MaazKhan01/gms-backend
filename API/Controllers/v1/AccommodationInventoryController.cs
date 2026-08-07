using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Accommodation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

// Accommodation inventory: which hotels an event is contracted with, and how many
// rooms of each type it holds on which nights. Nested under the event because
// both only exist in an event's context — hotels and room types themselves stay
// global lookups (/v1/lookups/hotels, /v1/lookups/room-types).
//
// Reads open to any authenticated user (the booking form needs the hotel and
// room-type dropdowns plus the availability calendar); writes need Travel.Manage.
[Route("api/v1/events/{eventId:guid}/accommodation")]
[Authorize]
[ApiVersion("1.0")]
public class AccommodationInventoryController(
    IAccommodationInventoryService _inventory, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // ── Contracts ────────────────────────────────────────────────────────────

    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts(Guid eventId, CancellationToken ct)
        => ToResponse(await _inventory.GetContractsAsync(eventId, ct));

    [HttpPost("contracts")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateContract(Guid eventId, [FromBody] CreateHotelContractRequest request, CancellationToken ct)
        => ToResponse(await _inventory.CreateContractAsync(eventId, request, _currentUser.UserId, ct));

    [HttpPut("contracts/{contractId:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> UpdateContract(Guid eventId, Guid contractId, [FromBody] UpdateHotelContractRequest request, CancellationToken ct)
        => ToResponse(await _inventory.UpdateContractAsync(eventId, contractId, request, _currentUser.UserId, ct));

    [HttpDelete("contracts/{contractId:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> DeleteContract(Guid eventId, Guid contractId, CancellationToken ct)
        => ToResponse(await _inventory.DeleteContractAsync(eventId, contractId, _currentUser.UserId, ct));

    // ── Room blocks ──────────────────────────────────────────────────────────

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(Guid eventId, [FromQuery] Guid? hotelId, CancellationToken ct)
        => ToResponse(await _inventory.GetInventoryAsync(eventId, hotelId, ct));

    [HttpPost("inventory")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateInventory(Guid eventId, [FromBody] CreateRoomInventoryRequest request, CancellationToken ct)
        => ToResponse(await _inventory.CreateInventoryAsync(eventId, request, _currentUser.UserId, ct));

    [HttpPut("inventory/{inventoryId:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> UpdateInventory(Guid eventId, Guid inventoryId, [FromBody] UpdateRoomInventoryRequest request, CancellationToken ct)
        => ToResponse(await _inventory.UpdateInventoryAsync(eventId, inventoryId, request, _currentUser.UserId, ct));

    /// <summary>Set the rooms held on one night of a block — the availability
    /// grid's editable cells. Splits the block so the other nights keep their
    /// count.</summary>
    [HttpPut("inventory/{inventoryId:guid}/night")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> SetNightRoomCount(Guid eventId, Guid inventoryId, [FromBody] SetNightRoomCountRequest request, CancellationToken ct)
        => ToResponse(await _inventory.SetNightRoomCountAsync(eventId, inventoryId, request, _currentUser.UserId, ct));

    [HttpDelete("inventory/{inventoryId:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> DeleteInventory(Guid eventId, Guid inventoryId, CancellationToken ct)
        => ToResponse(await _inventory.DeleteInventoryAsync(eventId, inventoryId, _currentUser.UserId, ct));

    // ── Booking-form feeds ───────────────────────────────────────────────────

    /// <summary>Hotels contracted for this event — the accommodation form's hotel list.</summary>
    [HttpGet("hotels")]
    public async Task<IActionResult> GetContractedHotels(Guid eventId, CancellationToken ct)
        => ToResponse(await _inventory.GetContractedHotelsAsync(eventId, ct));

    /// <summary>Room types with rooms held at this hotel. Empty = unmanaged hotel,
    /// so the caller should fall back to the global room-type lookup.</summary>
    [HttpGet("hotels/{hotelId:guid}/room-types")]
    public async Task<IActionResult> GetContractedRoomTypes(Guid eventId, Guid hotelId, CancellationToken ct)
        => ToResponse(await _inventory.GetContractedRoomTypesAsync(eventId, hotelId, ct));

    /// <summary>Per-night held / booked / left. One series per hotel + room type,
    /// plus per-night totals. Filter to a pair for the booking form's blocked
    /// dates; omit both for the Inventory screen's grid.</summary>
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        Guid eventId, [FromQuery] Guid? hotelId, [FromQuery] Guid? roomTypeId, CancellationToken ct)
        => ToResponse(await _inventory.GetAvailabilityAsync(eventId, hotelId, roomTypeId, ct));
}
