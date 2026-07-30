using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
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
    public async Task<ApiResponse<bool>> AssignSeatToGuestAsync(RequestSeatAssignDto request, int userId, CancellationToken ct)
    {
        try
        {
            if (request.SeatId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("SeatId is required.");
            if (request.GuestId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("GuestId is required.");
            if (request.EventId == null || request.EventId == Guid.Empty)
                return ApiResponse<bool>.ErrorResponse("EventId is required.");

            // userId is the internal acting-user id.
            if (userId == 0)
                return ApiResponse<bool>.ErrorResponse("Invalid user.");

            // Resolve the guest's public id to its internal id (used for FK/joins below).
            var guest = await _unitOfWork.Guests.Query()
                .FirstOrDefaultAsync(g => g.PublicId == request.GuestId, ct);
            if (guest == null)
                return ApiResponse<bool>.NotFoundResponse("Guest not found.");

            // Resolve the event's public id to its internal id.
            var eventEntity = await _unitOfWork.Events.Query()
                .FirstOrDefaultAsync(e => e.PublicId == request.EventId.Value, ct);
            if (eventEntity == null)
                return ApiResponse<bool>.NotFoundResponse("Event not found.");

            // Optional session — resolve its public id to an internal id when supplied.
            int? sessionId = null;
            if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
            {
                var session = await _unitOfWork.Sessions.Query()
                    .FirstOrDefaultAsync(s => s.PublicId == request.SessionId.Value, ct);
                if (session == null)
                    return ApiResponse<bool>.NotFoundResponse("Session not found.");
                sessionId = session.Id;
            }

            // Resolve the seat's venue box in one SQL round-trip — SeatProperties has
            // no navigation of its own back up to Venue, so the query has to originate
            // from VenueLayoutProp (which reaches VenueBox via either Layout or Block).
            var seatInfo = await _unitOfWork.VenueLayoutProps.Query()
                .SelectMany(p => p.Seats, (p, s) => new { Prop = p, Seat = s })
                .Where(x => x.Seat.PublicId == request.SeatId)
                .Select(x => new
                {
                    x.Seat.IsDisabled,
                    SeatId = x.Seat.Id,
                    VenueBoxId = x.Prop.Layout != null ? x.Prop.Layout.VenueBoxId
                        : (x.Prop.Block != null ? x.Prop.Block.VenueBoxId : (int?)null),
                })
                .FirstOrDefaultAsync(ct);

            if (seatInfo?.VenueBoxId == null)
                return ApiResponse<bool>.NotFoundResponse("Seat not found.");
            if (seatInfo.IsDisabled)
                return ApiResponse<bool>.ConflictResponse("This seat is disabled and cannot be assigned.");

            var venueBoxId = seatInfo.VenueBoxId.Value;
            var seatId = seatInfo.SeatId;
            var venueId = await _unitOfWork.VenueBoxes.Query()
                .Where(b => b.Id == venueBoxId)
                .Select(b => b.VenueId)
                .FirstOrDefaultAsync(ct);

            // A "Seating" is the assignment plan for one (event/session, venue box) —
            // find it, or create it the first time a seat is assigned under this scope.
            var seating = await _unitOfWork.Seatings.Query()
                .FirstOrDefaultAsync(s => s.VenueBoxId == venueBoxId
                    && s.EventId == eventEntity.Id
                    && s.EventSessionId == sessionId, ct);

            if (seating == null)
            {
                seating = new Seating
                {
                    EventId = eventEntity.Id,
                    VenueId = venueId,
                    VenueBoxId = venueBoxId,
                    EventSessionId = sessionId,
                };
                seating.SetCreationAudit(userId);
                await _unitOfWork.Seatings.AddAsync(seating, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // Moving a guest: drop whatever seat they previously held in this seating.
            var existingForGuest = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => sa.SeatingId == seating.Id && sa.GuestId == guest.Id)
                .ToListAsync(ct);
            if (existingForGuest.Count > 0)
                _unitOfWork.SeatAssigns.RemoveRange(existingForGuest);

            // Refuse to silently bump a different guest already in this seat.
            var existingForSeat = await _unitOfWork.SeatAssigns.Query()
                .FirstOrDefaultAsync(sa => sa.SeatingId == seating.Id && sa.SeatId == seatId, ct);
            if (existingForSeat != null && existingForSeat.GuestId != guest.Id)
                return ApiResponse<bool>.ConflictResponse("This seat is already assigned to another guest.");

            if (existingForSeat == null)
            {
                var assign = new SeatAssign
                {
                    SeatingId = seating.Id,
                    GuestId = guest.Id,
                    SeatId = seatId,
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

            // Resolve public ids to internal ids. If any scope entity is missing there
            // can be no matching seating, so the seat is effectively already unassigned.
            var venueBox = await _unitOfWork.VenueBoxes.Query()
                .FirstOrDefaultAsync(b => b.PublicId == venueBoxId, ct);
            var eventEntity = await _unitOfWork.Events.Query()
                .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
            var seat = await _unitOfWork.SeatProperties.Query()
                .FirstOrDefaultAsync(sp => sp.PublicId == seatId, ct);
            if (venueBox == null || eventEntity == null || seat == null)
                return ApiResponse<bool>.SuccessResponse(true, "Seat was already unassigned.");

            int? sessionIntId = null;
            if (sessionId.HasValue && sessionId.Value != Guid.Empty)
            {
                var session = await _unitOfWork.Sessions.Query()
                    .FirstOrDefaultAsync(s => s.PublicId == sessionId.Value, ct);
                if (session == null)
                    return ApiResponse<bool>.SuccessResponse(true, "Seat was already unassigned.");
                sessionIntId = session.Id;
            }

            var seating = await _unitOfWork.Seatings.Query()
                .FirstOrDefaultAsync(s => s.VenueBoxId == venueBox.Id && s.EventId == eventEntity.Id && s.EventSessionId == sessionIntId, ct);
            if (seating == null)
                return ApiResponse<bool>.SuccessResponse(true, "Seat was already unassigned.");

            var assign = await _unitOfWork.SeatAssigns.Query()
                .FirstOrDefaultAsync(sa => sa.SeatingId == seating.Id && sa.SeatId == seat.Id, ct);
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

            var venueBox = await _unitOfWork.VenueBoxes.Query()
                .FirstOrDefaultAsync(b => b.PublicId == venueBoxId, ct);
            var eventEntity = await _unitOfWork.Events.Query()
                .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
            if (venueBox == null || eventEntity == null)
                return ApiResponse<List<SeatAssignmentDto>>.SuccessResponse(new List<SeatAssignmentDto>());

            int? sessionIntId = null;
            if (sessionId.HasValue && sessionId.Value != Guid.Empty)
            {
                var session = await _unitOfWork.Sessions.Query()
                    .FirstOrDefaultAsync(s => s.PublicId == sessionId.Value, ct);
                if (session == null)
                    return ApiResponse<List<SeatAssignmentDto>>.SuccessResponse(new List<SeatAssignmentDto>());
                sessionIntId = session.Id;
            }

            var seating = await _unitOfWork.Seatings.Query()
                .FirstOrDefaultAsync(s => s.VenueBoxId == venueBox.Id && s.EventId == eventEntity.Id && s.EventSessionId == sessionIntId, ct);
            if (seating == null)
                return ApiResponse<List<SeatAssignmentDto>>.SuccessResponse(new List<SeatAssignmentDto>());

            // Output the related entities' PUBLIC ids (navigations), not the internal FK ints.
            var list = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => sa.SeatingId == seating.Id)
                .Select(sa => new SeatAssignmentDto { SeatId = sa.Seat.PublicId, GuestId = sa.Guest.PublicId })
                .ToListAsync(ct);

            return ApiResponse<List<SeatAssignmentDto>>.SuccessResponse(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading seat assignments for box {VenueBoxId}", venueBoxId);
            return ApiResponse<List<SeatAssignmentDto>>.ServerErrorResponse("An error occurred while loading seat assignments.");
        }
    }

    // Used by the Guests screen to warn before deleting a guest who still holds
    // a seat — surfaces exactly which event/session/seat so the confirmation
    // can name them, matching the wording used for the delete itself (the seat
    // is freed automatically when the guest is deleted — see Guest.DeleteGuestByIdAsync).
    public async Task<ApiResponse<List<GuestSeatAssignmentDto>>> GetGuestSeatAssignmentsAsync(Guid guestId, CancellationToken ct)
    {
        try
        {
            if (guestId == Guid.Empty)
                return ApiResponse<List<GuestSeatAssignmentDto>>.ErrorResponse("GuestId is required.");

            var list = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => sa.Guest.PublicId == guestId)
                .Select(sa => new GuestSeatAssignmentDto
                {
                    EventTitle = sa.Seating.Event.Title,
                    SessionTitle = sa.Seating.Session != null ? sa.Seating.Session.Title : null,
                    SeatCode = sa.Seat.Code,
                })
                .ToListAsync(ct);

            return ApiResponse<List<GuestSeatAssignmentDto>>.SuccessResponse(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading seat assignments for guest {GuestId}", guestId);
            return ApiResponse<List<GuestSeatAssignmentDto>>.ServerErrorResponse("An error occurred while loading the guest's seat assignments.");
        }
    }
}
