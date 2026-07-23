using System;
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

                var guest = await _unitOfWork.Guests.Query()
                    .FirstOrDefaultAsync(g => g.InvitationToken == token, ct);

                if (guest == null)
                    return ApiResponse<InvitationDetailResponse>.NotFoundResponse("This invitation link is invalid or has expired.");

                // Once a final decision is made it can't be flipped from the public
                // page — an admin can still change it from the guest editor.
                var alreadyFinal = guest.InvitationStatus == GuestInvitationStatus.Accepted
                                || guest.InvitationStatus == GuestInvitationStatus.Declined;
                if (alreadyFinal)
                    return ApiResponse<InvitationDetailResponse>.ConflictResponse(
                        "You have already responded to this invitation.");

                guest.InvitationStatus = request.Accept
                    ? GuestInvitationStatus.Accepted
                    : GuestInvitationStatus.Declined;
                _unitOfWork.Guests.Update(guest);
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

        // Guest has no Event navigation property, so the event is fetched separately by EventId.
        private async Task<InvitationDetailResponse> BuildDetailAsync(Guid token, CancellationToken ct)
        {
            var guest = await _unitOfWork.Guests.Query()
                .FirstOrDefaultAsync(g => g.InvitationToken == token, ct);
            if (guest == null) return null;

            var ev = await _unitOfWork.Events.Query()
                .FirstOrDefaultAsync(e => e.Id == guest.EventId, ct);

            return new InvitationDetailResponse
            {
                GuestName = $"{guest.FirstName} {guest.LastName}".Trim(),
                Tier = guest.Tier,
                EventTitle = ev?.Title,
                EventVenue = ev?.VenueName,
                EventStartDate = ev?.StartDate,
                EventEndDate = ev?.EndDate,
                InvitationStatus = guest.InvitationStatus,
                AlreadyResponded = guest.InvitationStatus == GuestInvitationStatus.Accepted
                                || guest.InvitationStatus == GuestInvitationStatus.Declined,
            };
        }
    }
}
