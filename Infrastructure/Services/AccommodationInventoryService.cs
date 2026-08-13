using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Accommodation;
using Core.ViewModel.Common;
using Core.ViewModel.Travel;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Per-event hotel contracts, the room blocks held under them, and the per-night
// arithmetic that stops a room being sold twice.
//
// Nights, not dates: a stay of CheckIn 5 → CheckOut 7 occupies nights 5 and 6.
// Inventory windows are inclusive on both ends (FromDate..ToDate are nights
// held), so the two line up without an off-by-one.
public class AccommodationInventoryService(
    IUnitOfWork _unitOfWork,
    ILogger<AccommodationInventoryService> _logger) : IAccommodationInventoryService
{
    // A block's window and size, and a stay's span — the two shapes every
    // calculation below works on, pulled out of the DB once.
    private record Block(DateOnly FromDate, DateOnly ToDate, int RoomCount);
    private record Stay(DateOnly CheckIn, DateOnly CheckOut);

    // ── Contracts ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<HotelContractResponse>>> GetContractsAsync(Guid eventId, CancellationToken ct = default)
    {
        var data = await _unitOfWork.EventHotelContracts.Query()
            .Where(c => c.Event.PublicId == eventId)
            .OrderBy(c => c.Hotel.Name)
            .Select(ProjectContract)
            .ToListAsync(ct);

        return ApiResponse<List<HotelContractResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<HotelContractResponse>> CreateContractAsync(
        Guid eventId, CreateHotelContractRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null) return ApiResponse<HotelContractResponse>.NotFoundResponse("Event not found");

            var hotel = await _unitOfWork.AccommodationHotels.GetByPublicIdAsync(request.HotelId, ct);
            if (hotel == null) return ApiResponse<HotelContractResponse>.ErrorResponse("Hotel not found");

            var exists = await _unitOfWork.EventHotelContracts.Query()
                .AnyAsync(c => c.EventId == ev.Id && c.AccommodationHotelId == hotel.Id, ct);
            if (exists) return ApiResponse<HotelContractResponse>.ErrorResponse("This hotel is already contracted for this event");

            var contract = new EventHotelContract
            {
                EventId = ev.Id,
                AccommodationHotelId = hotel.Id,
                Notes = Clean(request.Notes),
            };
            contract.SetCreationAudit(userId);
            await _unitOfWork.EventHotelContracts.AddAsync(contract, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetContractAsync(contract.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hotel contract for event {EventId}", eventId);
            return ApiResponse<HotelContractResponse>.ServerErrorResponse("An error occurred while creating the contract");
        }
    }

    public async Task<ApiResponse<HotelContractResponse>> UpdateContractAsync(
        Guid eventId, Guid contractId, UpdateHotelContractRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var contract = await FindContractAsync(eventId, contractId, ct);
            if (contract == null) return ApiResponse<HotelContractResponse>.NotFoundResponse("Contract not found");

            contract.Notes = Clean(request.Notes);
            contract.SetUpdateAudit(userId);
            _unitOfWork.EventHotelContracts.Update(contract);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetContractAsync(contract.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hotel contract {ContractId}", contractId);
            return ApiResponse<HotelContractResponse>.ServerErrorResponse("An error occurred while updating the contract");
        }
    }

    public async Task<ApiResponse<bool>> DeleteContractAsync(Guid eventId, Guid contractId, int userId, CancellationToken ct = default)
    {
        try
        {
            var contract = await FindContractAsync(eventId, contractId, ct);
            if (contract == null) return ApiResponse<bool>.NotFoundResponse("Contract not found");

            // Dropping the contract would strand every stay booked at this hotel:
            // its room blocks go with it, so those bookings lose their capacity.
            var booked = await _unitOfWork.Accommodations.Query()
                .AnyAsync(a => a.EventGuest.EventId == contract.EventId
                    && a.AccommodationHotelId == contract.AccommodationHotelId, ct);
            if (booked)
                return ApiResponse<bool>.ErrorResponse("Guests are already booked at this hotel for this event");

            // Blocks cascade in the DB, but they're soft-deleted rows here — mark
            // them so they stop counting toward capacity.
            var blocks = await _unitOfWork.HotelRoomInventories.Query()
                .Where(i => i.EventHotelContractId == contract.Id)
                .ToListAsync(ct);
            foreach (var block in blocks)
            {
                block.MarkAsDeleted(userId);
                _unitOfWork.HotelRoomInventories.Update(block);
            }

            contract.MarkAsDeleted(userId);
            _unitOfWork.EventHotelContracts.Update(contract);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Contract deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hotel contract {ContractId}", contractId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the contract");
        }
    }

    // ── Inventory ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<RoomInventoryResponse>>> GetInventoryAsync(
        Guid eventId, Guid? hotelId = null, CancellationToken ct = default)
    {
        var query = _unitOfWork.HotelRoomInventories.Query()
            .Where(i => i.Contract.Event.PublicId == eventId);

        if (hotelId is { } hId && hId != Guid.Empty)
            query = query.Where(i => i.Contract.Hotel.PublicId == hId);

        var data = await query
            .OrderBy(i => i.Contract.Hotel.Name).ThenBy(i => i.RoomType.Name).ThenBy(i => i.FromDate)
            .Select(ProjectInventory)
            .ToListAsync(ct);

        return ApiResponse<List<RoomInventoryResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<RoomInventoryResponse>> CreateInventoryAsync(
        Guid eventId, CreateRoomInventoryRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var contract = await FindContractAsync(eventId, request.ContractId, ct);
            if (contract == null) return ApiResponse<RoomInventoryResponse>.ErrorResponse("Contract not found for this event");

            var error = Validate(request.RoomCount, request.FromDate, request.ToDate);
            if (error != null) return ApiResponse<RoomInventoryResponse>.ErrorResponse(error);

            var roomType = await _unitOfWork.AccommodationRoomTypes.GetByPublicIdAsync(request.RoomTypeId, ct);
            if (roomType == null) return ApiResponse<RoomInventoryResponse>.ErrorResponse("Room type not found");

            var block = new HotelRoomInventory
            {
                EventHotelContractId = contract.Id,
                RoomTypeId = roomType.Id,
                RoomCount = request.RoomCount,
                FromDate = request.FromDate.Value,
                ToDate = request.ToDate.Value,
                Notes = Clean(request.Notes),
            };
            block.SetCreationAudit(userId);
            await _unitOfWork.HotelRoomInventories.AddAsync(block, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetInventoryRowAsync(block.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room inventory for event {EventId}", eventId);
            return ApiResponse<RoomInventoryResponse>.ServerErrorResponse("An error occurred while creating the room block");
        }
    }

    public async Task<ApiResponse<RoomInventoryResponse>> UpdateInventoryAsync(
        Guid eventId, Guid inventoryId, UpdateRoomInventoryRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var block = await FindInventoryAsync(eventId, inventoryId, ct);
            if (block == null) return ApiResponse<RoomInventoryResponse>.NotFoundResponse("Room block not found");

            var error = Validate(request.RoomCount, request.FromDate, request.ToDate);
            if (error != null) return ApiResponse<RoomInventoryResponse>.ErrorResponse(error);

            var roomType = await _unitOfWork.AccommodationRoomTypes.GetByPublicIdAsync(request.RoomTypeId, ct);
            if (roomType == null) return ApiResponse<RoomInventoryResponse>.ErrorResponse("Room type not found");

            // Shrinking or moving a block can leave nights it used to cover with
            // fewer rooms than are already booked on them. Check before saving,
            // against the block list as it would look afterwards.
            var breach = await FindBreachAsync(block, roomType.Id, request.RoomCount,
                request.FromDate.Value, request.ToDate.Value, ct);
            if (breach != null) return ApiResponse<RoomInventoryResponse>.ErrorResponse(breach);

            block.RoomTypeId = roomType.Id;
            block.RoomCount = request.RoomCount;
            block.FromDate = request.FromDate.Value;
            block.ToDate = request.ToDate.Value;
            block.Notes = Clean(request.Notes);

            block.SetUpdateAudit(userId);
            _unitOfWork.HotelRoomInventories.Update(block);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetInventoryRowAsync(block.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room inventory {InventoryId}", inventoryId);
            return ApiResponse<RoomInventoryResponse>.ServerErrorResponse("An error occurred while updating the room block");
        }
    }

    public async Task<ApiResponse<bool>> DeleteInventoryAsync(Guid eventId, Guid inventoryId, int userId, CancellationToken ct = default)
    {
        try
        {
            var block = await FindInventoryAsync(eventId, inventoryId, ct);
            if (block == null) return ApiResponse<bool>.NotFoundResponse("Room block not found");

            // Same guard as update, with the block removed entirely (RoomCount 0).
            var breach = await FindBreachAsync(block, block.RoomTypeId, 0, block.FromDate, block.ToDate, ct);
            if (breach != null) return ApiResponse<bool>.ErrorResponse(breach);

            block.MarkAsDeleted(userId);
            _unitOfWork.HotelRoomInventories.Update(block);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Room block deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room inventory {InventoryId}", inventoryId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the room block");
        }
    }

    public async Task<ApiResponse<bool>> SetNightRoomCountAsync(
        Guid eventId, Guid inventoryId, SetNightRoomCountRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var block = await FindInventoryAsync(eventId, inventoryId, ct);
            if (block == null) return ApiResponse<bool>.NotFoundResponse("Room block not found");

            if (request.Date == null) return ApiResponse<bool>.ErrorResponse("Date is required");
            var night = request.Date.Value;
            if (night < block.FromDate || night > block.ToDate)
                return ApiResponse<bool>.ErrorResponse("That night is outside this room block");
            if (request.RoomCount < 0) return ApiResponse<bool>.ErrorResponse("Number of rooms cannot be negative");
            if (request.RoomCount == block.RoomCount) return ApiResponse<bool>.SuccessResponse(true, "No change");

            // What this block becomes: the edited night on its own, the untouched
            // ends carrying their old count. Zero rooms drops the middle piece —
            // the night is then simply not held.
            var after = new List<Block>();
            if (night > block.FromDate) after.Add(new Block(block.FromDate, night.AddDays(-1), block.RoomCount));
            if (request.RoomCount > 0) after.Add(new Block(night, night, request.RoomCount));
            if (night < block.ToDate) after.Add(new Block(night.AddDays(1), block.ToDate, block.RoomCount));

            var breach = await FindBreachAsync(block, block.RoomTypeId, after, ct);
            if (breach != null) return ApiResponse<bool>.ErrorResponse(breach);

            // The existing row narrows to the edited night rather than being
            // replaced, so its id (and audit trail) stays with the change that was
            // actually made; the ends become new rows. One SaveChanges, so a
            // failure can't leave the block half-split.
            if (request.RoomCount > 0)
            {
                block.FromDate = night;
                block.ToDate = night;
                block.RoomCount = request.RoomCount;
                block.SetUpdateAudit(userId);
            }
            else
            {
                block.MarkAsDeleted(userId);
            }
            _unitOfWork.HotelRoomInventories.Update(block);

            foreach (var seg in after.Where(s => s.FromDate != night || s.ToDate != night))
            {
                var row = new HotelRoomInventory
                {
                    EventHotelContractId = block.EventHotelContractId,
                    RoomTypeId = block.RoomTypeId,
                    RoomCount = seg.RoomCount,
                    FromDate = seg.FromDate,
                    ToDate = seg.ToDate,
                    Notes = block.Notes,
                };
                row.SetCreationAudit(userId);
                await _unitOfWork.HotelRoomInventories.AddAsync(row, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, "Rooms updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting night room count on inventory {InventoryId}", inventoryId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while updating the room block");
        }
    }

    // ── Booking-form feeds ───────────────────────────────────────────────────

    public async Task<ApiResponse<List<HotelDto>>> GetContractedHotelsAsync(Guid eventId, CancellationToken ct = default)
    {
        var data = await _unitOfWork.EventHotelContracts.Query()
            .Where(c => c.Event.PublicId == eventId)
            .OrderBy(c => c.Hotel.Name)
            .Select(c => new HotelDto
            {
                Id = c.Hotel.PublicId,
                Name = c.Hotel.Name,
                Address = c.Hotel.Address,
                ImageUrl = c.Hotel.ImageUrl,
                LocationId = c.Hotel.Location == null ? null : (Guid?)c.Hotel.Location.PublicId,
            })
            .ToListAsync(ct);

        return ApiResponse<List<HotelDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<IdNameDto>>> GetContractedRoomTypesAsync(
        Guid eventId, Guid hotelId, CancellationToken ct = default)
    {
        // Distinct on the two raw columns (several blocks may hold the same room
        // type over different windows), then shaped client-side — Distinct over a
        // projected DTO isn't reliably translatable.
        var rows = await _unitOfWork.HotelRoomInventories.Query()
            .Where(i => i.Contract.Event.PublicId == eventId && i.Contract.Hotel.PublicId == hotelId)
            .Select(i => new { i.RoomType.PublicId, i.RoomType.Name })
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var data = rows.Select(x => new IdNameDto { Id = x.PublicId, Name = x.Name }).ToList();
        return ApiResponse<List<IdNameDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<RoomAvailabilityResponse>> GetAvailabilityAsync(
        Guid eventId, Guid? hotelId = null, Guid? roomTypeId = null, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<RoomAvailabilityResponse>.NotFoundResponse("Event not found");

        // An empty query-string Guid binds as Guid.Empty — same intent as absent.
        if (hotelId == Guid.Empty) hotelId = null;
        if (roomTypeId == Guid.Empty) roomTypeId = null;

        // Two flat reads for the whole event, then the arithmetic in memory. A grid
        // over every hotel × room type would otherwise be one query pair per cell.
        var blocks = await _unitOfWork.HotelRoomInventories.Query()
            .Where(i => i.Contract.EventId == ev.Id
                && (hotelId == null || i.Contract.Hotel.PublicId == hotelId)
                && (roomTypeId == null || i.RoomType.PublicId == roomTypeId))
            .Select(i => new
            {
                HotelId = i.Contract.Hotel.PublicId,
                HotelName = i.Contract.Hotel.Name,
                RoomTypeId = i.RoomType.PublicId,
                RoomTypeName = i.RoomType.Name,
                i.FromDate,
                i.ToDate,
                i.RoomCount,
            })
            .ToListAsync(ct);

        var result = new RoomAvailabilityResponse();
        if (blocks.Count == 0) return ApiResponse<RoomAvailabilityResponse>.SuccessResponse(result);

        var stays = await _unitOfWork.Accommodations.Query()
            .Where(a => a.EventGuest.EventId == ev.Id
                && a.CheckIn != null && a.CheckOut != null
                && a.RoomTypeId != null
                && (hotelId == null || a.Hotel.PublicId == hotelId)
                && (roomTypeId == null || a.RoomType.PublicId == roomTypeId))
            .Select(a => new
            {
                HotelId = a.Hotel.PublicId,
                RoomTypeId = a.RoomType.PublicId,
                CheckIn = a.CheckIn.Value,
                CheckOut = a.CheckOut.Value,
            })
            .ToListAsync(ct);

        // Every series spans the same window — one shared date axis is what makes
        // the grid's columns line up.
        var first = blocks.Min(b => b.FromDate);
        var last = blocks.Max(b => b.ToDate);
        result.From = first;
        result.To = last;

        foreach (var pair in blocks
            .GroupBy(b => new { b.HotelId, b.HotelName, b.RoomTypeId, b.RoomTypeName })
            .OrderBy(g => g.Key.HotelName).ThenBy(g => g.Key.RoomTypeName))
        {
            var pairBlocks = pair.Select(b => new Block(b.FromDate, b.ToDate, b.RoomCount)).ToList();
            var pairStays = stays
                .Where(s => s.HotelId == pair.Key.HotelId && s.RoomTypeId == pair.Key.RoomTypeId)
                .Select(s => new Stay(s.CheckIn, s.CheckOut))
                .ToList();

            var series = new RoomAvailabilitySeries
            {
                HotelId = pair.Key.HotelId,
                HotelName = pair.Key.HotelName,
                RoomTypeId = pair.Key.RoomTypeId,
                RoomTypeName = pair.Key.RoomTypeName,
            };

            for (var night = first; night <= last; night = night.AddDays(1))
            {
                var total = Held(pairBlocks, night);
                // Nights outside this pair's own blocks hold nothing, and a stay
                // there is already a breach — don't hide it behind a zero.
                var booked = Taken(pairStays, night);
                series.Nights.Add(new RoomAvailabilityNight
                {
                    Date = night,
                    Total = total,
                    Booked = booked,
                    Available = total - booked,
                });
            }

            result.Series.Add(series);
        }

        return ApiResponse<RoomAvailabilityResponse>.SuccessResponse(result);
    }

    // ── Enforcement ──────────────────────────────────────────────────────────

    public async Task<string> CheckStayAvailabilityAsync(
        int eventId, int hotelId, int? roomTypeId, DateOnly checkIn, DateOnly checkOut,
        int? excludeAccommodationId = null, CancellationToken ct = default)
    {
        if (checkOut <= checkIn) return "Check-out must be after check-in";

        // A hotel with no blocks at all is unmanaged: events that predate this
        // module (or that don't hold rooms) keep booking freely. Room type is only
        // required once someone starts holding rooms there.
        var hotelIsManaged = await _unitOfWork.HotelRoomInventories.Query()
            .AnyAsync(i => i.Contract.EventId == eventId && i.Contract.AccommodationHotelId == hotelId, ct);
        if (!hotelIsManaged) return null;

        if (roomTypeId == null)
            return "Room type is required — this hotel's rooms are managed as inventory for this event";

        var blocks = await LoadBlocksAsync(eventId, hotelId, roomTypeId.Value, ct);
        if (blocks.Count == 0)
            return "No rooms of this type are held at this hotel for this event";

        var stays = await LoadStaysAsync(eventId, hotelId, roomTypeId.Value, excludeAccommodationId, ct);

        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            var total = Held(blocks, night);
            var booked = Taken(stays, night);
            if (booked + 1 > total)
            {
                return total == 0
                    ? $"No rooms of this type are held for the night of {night:dd-MMM-yyyy}"
                    : $"All {total} room(s) of this type are taken on the night of {night:dd-MMM-yyyy} ({booked}/{total}). Pick different dates or another room type.";
            }
        }

        return null;
    }

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>Rooms held on one night — overlapping blocks add up.</summary>
    private static int Held(List<Block> blocks, DateOnly night)
        => blocks.Where(b => night >= b.FromDate && night <= b.ToDate).Sum(b => b.RoomCount);

    /// <summary>Rooms taken on one night. A stay occupies [CheckIn, CheckOut) —
    /// the check-out day is not a night slept.</summary>
    private static int Taken(List<Stay> stays, DateOnly night)
        => stays.Count(s => night >= s.CheckIn && night < s.CheckOut);

    private async Task<List<Block>> LoadBlocksAsync(int eventId, int hotelId, int roomTypeId, CancellationToken ct)
        => await _unitOfWork.HotelRoomInventories.Query()
            .Where(i => i.Contract.EventId == eventId
                && i.Contract.AccommodationHotelId == hotelId
                && i.RoomTypeId == roomTypeId)
            .Select(i => new Block(i.FromDate, i.ToDate, i.RoomCount))
            .ToListAsync(ct);

    private async Task<List<Stay>> LoadStaysAsync(
        int eventId, int hotelId, int roomTypeId, int? excludeAccommodationId, CancellationToken ct)
        => await _unitOfWork.Accommodations.Query()
            .Where(a => a.EventGuest.EventId == eventId
                && a.AccommodationHotelId == hotelId
                && a.RoomTypeId == roomTypeId
                && a.CheckIn != null && a.CheckOut != null
                && (excludeAccommodationId == null || a.Id != excludeAccommodationId))
            .Select(a => new Stay(a.CheckIn.Value, a.CheckOut.Value))
            .ToListAsync(ct);

    /// <summary>Would this edit leave a night short? Recomputes the affected room
    /// type's capacity with `block` replaced by (newRoomCount, newFrom..newTo) —
    /// pass 0 rooms to model a delete — and reports the first night where
    /// bookings would exceed it.</summary>
    private Task<string> FindBreachAsync(
        HotelRoomInventory block, int newRoomTypeId, int newRoomCount,
        DateOnly newFrom, DateOnly newTo, CancellationToken ct)
        => FindBreachAsync(block, newRoomTypeId,
            newRoomCount > 0 ? [new Block(newFrom, newTo, newRoomCount)] : [],
            ct);

    /// <summary>Same check, for edits that leave more than one piece behind (a
    /// per-night change splits a block in up to three). `replacements` is what
    /// `block` becomes; an empty list models a delete. Only the block's OLD
    /// window is at risk: nights outside it keep the capacity they had.</summary>
    private async Task<string> FindBreachAsync(
        HotelRoomInventory block, int newRoomTypeId, List<Block> replacements, CancellationToken ct)
    {
        var contract = await _unitOfWork.EventHotelContracts.Query()
            .FirstOrDefaultAsync(c => c.Id == block.EventHotelContractId, ct);
        if (contract == null) return null;

        // Changing a block's room type drops capacity from the old type, so that's
        // the side to check.
        var affectedRoomTypeId = block.RoomTypeId;

        var others = await _unitOfWork.HotelRoomInventories.Query()
            .Where(i => i.EventHotelContractId == block.EventHotelContractId
                && i.RoomTypeId == affectedRoomTypeId
                && i.Id != block.Id)
            .Select(i => new Block(i.FromDate, i.ToDate, i.RoomCount))
            .ToListAsync(ct);

        if (newRoomTypeId == affectedRoomTypeId)
            others.AddRange(replacements);

        var stays = await LoadStaysAsync(contract.EventId, contract.AccommodationHotelId, affectedRoomTypeId, null, ct);

        for (var night = block.FromDate; night <= block.ToDate; night = night.AddDays(1))
        {
            var booked = Taken(stays, night);
            if (booked > Held(others, night))
                return $"{booked} guest(s) are already booked into this room type on the night of {night:dd-MMM-yyyy} — free them up first.";
        }

        return null;
    }

    private static string Validate(int roomCount, DateOnly? fromDate, DateOnly? toDate)
    {
        if (roomCount <= 0) return "Number of rooms must be greater than zero";
        if (fromDate == null) return "From date is required";
        if (toDate == null) return "To date is required";
        if (toDate < fromDate) return "The last night cannot be before the first night";
        return null;
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task<EventHotelContract> FindContractAsync(Guid eventId, Guid contractId, CancellationToken ct)
        => _unitOfWork.EventHotelContracts.Query()
            .FirstOrDefaultAsync(c => c.PublicId == contractId && c.Event.PublicId == eventId, ct);

    private Task<HotelRoomInventory> FindInventoryAsync(Guid eventId, Guid inventoryId, CancellationToken ct)
        => _unitOfWork.HotelRoomInventories.Query()
            .FirstOrDefaultAsync(i => i.PublicId == inventoryId && i.Contract.Event.PublicId == eventId, ct);

    private async Task<ApiResponse<HotelContractResponse>> GetContractAsync(Guid contractPublicId, CancellationToken ct)
    {
        var row = await _unitOfWork.EventHotelContracts.Query()
            .Where(c => c.PublicId == contractPublicId)
            .Select(ProjectContract)
            .FirstOrDefaultAsync(ct);

        return row == null
            ? ApiResponse<HotelContractResponse>.NotFoundResponse("Contract not found")
            : ApiResponse<HotelContractResponse>.SuccessResponse(row);
    }

    private async Task<ApiResponse<RoomInventoryResponse>> GetInventoryRowAsync(Guid inventoryPublicId, CancellationToken ct)
    {
        var row = await _unitOfWork.HotelRoomInventories.Query()
            .Where(i => i.PublicId == inventoryPublicId)
            .Select(ProjectInventory)
            .FirstOrDefaultAsync(ct);

        return row == null
            ? ApiResponse<RoomInventoryResponse>.NotFoundResponse("Room block not found")
            : ApiResponse<RoomInventoryResponse>.SuccessResponse(row);
    }

    // Shared projections — expression trees so EF puts them in the SELECT.
    private static readonly System.Linq.Expressions.Expression<Func<EventHotelContract, HotelContractResponse>> ProjectContract =
        c => new HotelContractResponse
        {
            Id = c.PublicId,
            EventId = c.Event.PublicId,
            HotelId = c.Hotel.PublicId,
            HotelName = c.Hotel.Name,
            HotelAddress = c.Hotel.Address,
            HotelImageUrl = c.Hotel.ImageUrl,
            Notes = c.Notes,
            InventoryCount = c.Inventory.Count,
            // Nullable cast: SQL SUM over no rows is NULL, which won't land in an int.
            TotalRooms = c.Inventory.Sum(i => (int?)i.RoomCount) ?? 0,
        };

    private static readonly System.Linq.Expressions.Expression<Func<HotelRoomInventory, RoomInventoryResponse>> ProjectInventory =
        i => new RoomInventoryResponse
        {
            Id = i.PublicId,
            ContractId = i.Contract.PublicId,
            HotelId = i.Contract.Hotel.PublicId,
            HotelName = i.Contract.Hotel.Name,
            RoomTypeId = i.RoomType.PublicId,
            RoomTypeName = i.RoomType.Name,
            RoomCount = i.RoomCount,
            FromDate = i.FromDate,
            ToDate = i.ToDate,
            // Inclusive both ends, so a single-night block is 1 night, not 0.
            Nights = i.ToDate.DayNumber - i.FromDate.DayNumber + 1,
            Notes = i.Notes,
        };
}
