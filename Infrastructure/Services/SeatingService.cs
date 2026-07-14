using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Seating;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class SeatingService(IUnitOfWork _unitOfWork, ILogger<SeatingService> _logger) : ISeatingService
{
    public async Task<ApiResponse<bool>> AssignSeatToGuestAsync(RequestSeatAssignDto request, Guid userId, CancellationToken ct)
    {
        try
        {
            if (request.SeatId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("SeatId is required.");
            if (request.GuestId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("GuestId is required.");
            if (request.EventId == null || request.EventId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("EventId is required.");

            var guestExists = await _unitOfWork.Guests.Query().AnyAsync(g => g.Id == request.GuestId, ct);
            if (!guestExists)
                return ApiResponse<bool>.NotFoundResponse("Guest not found.");

            // Resolve the seat's venue box in one SQL round-trip — SeatProperties has
            // no navigation of its own back up to Venue, so the query has to originate
            // from VenueLayoutProp (which reaches VenueBox via either Layout or Block).
            var seatInfo = await _unitOfWork.VenueLayoutProps.Query()
                .SelectMany(p => p.Seats, (p, s) => new { Prop = p, Seat = s })
                .Where(x => x.Seat.Id == request.SeatId)
                .Select(x => new
                {
                    x.Seat.IsDisabled,
                    VenueBoxId = x.Prop.Layout != null ? x.Prop.Layout.VenueBoxId
                        : (x.Prop.Block != null ? x.Prop.Block.VenueBoxId : (Guid?)null),
                })
                .FirstOrDefaultAsync(ct);

            if (seatInfo?.VenueBoxId == null)
                return ApiResponse<bool>.NotFoundResponse("Seat not found.");
            if (seatInfo.IsDisabled)
                return ApiResponse<bool>.ConflictResponse("This seat is disabled and cannot be assigned.");

            var venueBoxId = seatInfo.VenueBoxId.Value;
            var venueId = await _unitOfWork.VenueBoxes.Query()
                .Where(b => b.Id == venueBoxId)
                .Select(b => b.VenueId)
                .FirstOrDefaultAsync(ct);

            // A "Seating" is the assignment plan for one (event/session, venue box) —
            // find it, or create it the first time a seat is assigned under this scope.
            var seating = await _unitOfWork.Seatings.Query()
                .FirstOrDefaultAsync(s => s.VenueBoxId == venueBoxId
                    && s.EventId == request.EventId
                    && s.EventSessionId == request.SessionId, ct);

            if (seating == null)
            {
                seating = new Seating
                {
                    Id = Guid.NewGuid(),
                    EventId = request.EventId.Value,
                    VenueId = venueId,
                    VenueBoxId = venueBoxId,
                    EventSessionId = request.SessionId,
                };
                seating.SetCreationAudit(userId);
                await _unitOfWork.Seatings.AddAsync(seating, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // Moving a guest: drop whatever seat they previously held in this seating.
            var existingForGuest = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => sa.SeatingId == seating.Id && sa.GuestId == request.GuestId)
                .ToListAsync(ct);
            if (existingForGuest.Count > 0)
                _unitOfWork.SeatAssigns.RemoveRange(existingForGuest);

            // Refuse to silently bump a different guest already in this seat.
            var existingForSeat = await _unitOfWork.SeatAssigns.Query()
                .FirstOrDefaultAsync(sa => sa.SeatingId == seating.Id && sa.SeatId == request.SeatId, ct);
            if (existingForSeat != null && existingForSeat.GuestId != request.GuestId)
                return ApiResponse<bool>.ConflictResponse("This seat is already assigned to another guest.");

            if (existingForSeat == null)
            {
                var assign = new SeatAssign
                {
                    Id = Guid.NewGuid(),
                    SeatingId = seating.Id,
                    GuestId = request.GuestId,
                    SeatId = request.SeatId,
                };
                assign.SetCreationAudit(userId);
                await _unitOfWork.SeatAssigns.AddAsync(assign, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, "Seat assigned.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning seat {SeatId} to guest {GuestId}", request.SeatId, request.GuestId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while assigning the seat.");
        }
    }

    public async Task<ApiResponse<bool>> UnassignSeatAsync(Guid seatId, Guid venueBoxId, Guid eventId, Guid? sessionId, CancellationToken ct)
    {
        try
        {
            if (seatId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("SeatId is required.");
            if (venueBoxId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("VenueBoxId is required.");
            if (eventId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("EventId is required.");

            var seating = await _unitOfWork.Seatings.Query()
                .FirstOrDefaultAsync(s => s.VenueBoxId == venueBoxId && s.EventId == eventId && s.EventSessionId == sessionId, ct);
            if (seating == null)
                return ApiResponse<bool>.SuccessResponse(true, "Seat was already unassigned.");

            var assign = await _unitOfWork.SeatAssigns.Query()
                .FirstOrDefaultAsync(sa => sa.SeatingId == seating.Id && sa.SeatId == seatId, ct);
            if (assign == null)
                return ApiResponse<bool>.SuccessResponse(true, "Seat was already unassigned.");

            _unitOfWork.SeatAssigns.Remove(assign);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, "Seat unassigned.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning seat {SeatId}", seatId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while unassigning the seat.");
        }
    }

    public async Task<ApiResponse<List<SeatAssignmentDto>>> GetSeatAssignmentsAsync(Guid venueBoxId, Guid eventId, Guid? sessionId, CancellationToken ct)
    {
        try
        {
            if (venueBoxId == Guid.Empty)
                return ApiResponse<List<SeatAssignmentDto>>.ErrorResponse("VenueBoxId is required.");
            if (eventId == Guid.Empty)
                return ApiResponse<List<SeatAssignmentDto>>.ErrorResponse("EventId is required.");

            var seating = await _unitOfWork.Seatings.Query()
                .FirstOrDefaultAsync(s => s.VenueBoxId == venueBoxId && s.EventId == eventId && s.EventSessionId == sessionId, ct);
            if (seating == null)
                return ApiResponse<List<SeatAssignmentDto>>.SuccessResponse(new List<SeatAssignmentDto>());

            var list = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => sa.SeatingId == seating.Id)
                .Select(sa => new SeatAssignmentDto { SeatId = sa.SeatId, GuestId = sa.GuestId })
                .ToListAsync(ct);

            return ApiResponse<List<SeatAssignmentDto>>.SuccessResponse(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading seat assignments for box {VenueBoxId}", venueBoxId);
            return ApiResponse<List<SeatAssignmentDto>>.ServerErrorResponse("An error occurred while loading seat assignments.");
        }
    }
}
