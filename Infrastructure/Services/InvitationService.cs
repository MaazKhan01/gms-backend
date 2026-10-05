using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Invitation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    // Public (no-login) accept/reject flow. Token + status now live on the
    // Invitation entity (split out of Guest).
    public class InvitationService(IUnitOfWork _unitOfWork, ILogger<InvitationService> _logger) : IInvitationService
    {
        public async Task<ApiResponse<InvitationDetailResponse>> GetByTokenAsync(Guid token, CancellationToken ct)
        {
            try
            {
                if (token == Guid.Empty)
                    return ApiResponse<InvitationDetailResponse>.NotFoundResponse("Invitation not found.");

                var dto = await BuildDetailAsync(token, ct);
                if (dto == null)
                    return ApiResponse<InvitationDetailResponse>.NotFoundResponse("This invitation link is invalid or has expired.");

                return ApiResponse<InvitationDetailResponse>.SuccessResponse(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invitation for token {Token}", token);
                return ApiResponse<InvitationDetailResponse>.ServerErrorResponse("An error occurred while loading the invitation.");
            }
        }

        public async Task<ApiResponse<InvitationDetailResponse>> RespondAsync(Guid token, RespondToInvitationRequest request, CancellationToken ct)
        {
            try
            {
                if (token == Guid.Empty)
                    return ApiResponse<InvitationDetailResponse>.NotFoundResponse("Invitation not found.");

                var invitation = await _unitOfWork.Invitations.Query()
                    .FirstOrDefaultAsync(i => i.InvitationToken == token, ct);

                if (invitation == null)
                    return ApiResponse<InvitationDetailResponse>.NotFoundResponse("This invitation link is invalid or has expired.");

                // Once a final decision is made it can't be flipped from the public
                // page — an admin can still change it from the guest editor.
                var alreadyFinal = invitation.InvitationStatus == GuestInvitationStatus.Accepted
                                || invitation.InvitationStatus == GuestInvitationStatus.Declined;
                if (alreadyFinal)
                    return ApiResponse<InvitationDetailResponse>.ConflictResponse(
                        "You have already responded to this invitation.");

                invitation.InvitationStatus = request.Accept
                    ? GuestInvitationStatus.Accepted
                    : GuestInvitationStatus.Declined;
                invitation.RespondedAt = DateTime.UtcNow;
                _unitOfWork.Invitations.Update(invitation);
                await _unitOfWork.SaveChangesAsync(ct);

                var dto = await BuildDetailAsync(token, ct);
                return ApiResponse<InvitationDetailResponse>.SuccessResponse(dto,
                    request.Accept ? "Invitation accepted." : "Invitation declined.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error responding to invitation for token {Token}", token);
                return ApiResponse<InvitationDetailResponse>.ServerErrorResponse("An error occurred while saving your response.");
            }
        }

        private async Task<InvitationDetailResponse> BuildDetailAsync(Guid token, CancellationToken ct)
        {
            // An invitation belongs to one participation, so the event, the level
            // and the dates all come off EventGuest; only the name/email/photo are
            // the person's.
            var invitation = await _unitOfWork.Invitations.Query()
                .Include(i => i.EventGuest).ThenInclude(eg => eg.Guest)
                .Include(i => i.EventGuest).ThenInclude(eg => eg.Event)
                .Include(i => i.EventGuest).ThenInclude(eg => eg.ServiceLevel).ThenInclude(l => l.Services).ThenInclude(a => a.Service)
                .FirstOrDefaultAsync(i => i.InvitationToken == token, ct);
            if (invitation?.EventGuest?.Guest == null) return null;

            var participation = invitation.EventGuest;
            var guest = participation.Guest;
            var ev = participation.Event;
            var level = participation.ServiceLevel;

            // Travel dates live on the flight bookings now, not on the participation.
            // Earliest landing / latest take-off, so a return booking reads as one
            // trip rather than two. Null when nothing is booked yet — the RSVP page
            // already renders that as a blank row.
            var flightTimes = await _unitOfWork.Flights.QueryNoTracking()
                .Where(f => f.EventGuestId == participation.Id)
                .Select(f => new { f.ArrivalTime, f.DepartureTime })
                .ToListAsync(ct);
            var arrival = flightTimes.Min(f => f.ArrivalTime);
            var departure = flightTimes.Max(f => f.DepartureTime);

            return new InvitationDetailResponse
            {
                GuestName = $"{guest.FirstName} {guest.LastName}".Trim(),
                GuestEmail = guest.Email,
                GuestPhotoUrl = guest.PhotoUrl,
                Organization = participation.Organization,
                Tier = level?.Code,
                ServiceLevelName = level?.Name,
                ServiceLevelNameAr = level?.NameAr,
                ServiceLevelColor = level?.Color,
                ArrivalDate = arrival.HasValue ? DateOnly.FromDateTime(arrival.Value) : null,
                DepartureDate = departure.HasValue ? DateOnly.FromDateTime(departure.Value) : null,
                EventTitle = ev?.Title,
                EventVenue = ev?.VenueName,
                EventStartDate = ev?.StartDate,
                EventEndDate = ev?.EndDate,
                Services = (level?.Services ?? new List<DomainPersistence.Entities.ServiceLevelService>())
                    .Where(a => a.Service != null)
                    .OrderBy(a => a.SortOrder)
                    .Select(a => new InvitationServiceItem
                    {
                        Name = a.Service.Name,
                        NameAr = a.Service.NameAr,
                        Icon = a.Service.Icon,
                    })
                    .ToList(),
                InvitationStatus = invitation.InvitationStatus,
                AlreadyResponded = invitation.InvitationStatus == GuestInvitationStatus.Accepted
                                || invitation.InvitationStatus == GuestInvitationStatus.Declined,
            };
        }
    }
}
