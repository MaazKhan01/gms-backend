using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Event;
using Core.ViewModel.HostInvitation;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 1 — invitations received from host organisations, and turning one into
/// a mission. Creating the mission goes through <see cref="IEventService"/> so
/// slug generation, venue resolution and status validation stay in one place.
/// </summary>
public class HostInvitationService(
    IUnitOfWork _unitOfWork,
    IEventService _events,
    ILogger<HostInvitationService> _logger) : IHostInvitationService
{
    public async Task<ApiResponse<PaginatedResponse<HostInvitationResponse>>> GetAllAsync(
        PagedRequest request, string status, CancellationToken ct = default)
    {
        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 20;

        var query = _unitOfWork.HostInvitations.QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            if (!HostInvitationStatuses.IsValid(s))
                return ApiResponse<PaginatedResponse<HostInvitationResponse>>.ErrorResponse(
                    $"Invalid status '{status}'. Expected logged, converted or declined.");
            query = query.Where(i => i.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(i =>
                i.MissionTitle.ToLower().Contains(term) ||
                i.HostOrganization.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(Projection)
            .ToListAsync(ct);

        return ApiResponse<PaginatedResponse<HostInvitationResponse>>.SuccessResponse(
            new PaginatedResponse<HostInvitationResponse>(items, total, page, size));
    }

    public async Task<ApiResponse<HostInvitationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<HostInvitationResponse>.ErrorResponse("Invitation id is required.");

        var data = await _unitOfWork.HostInvitations.QueryNoTracking()
            .Where(i => i.PublicId == id)
            .Select(Projection)
            .FirstOrDefaultAsync(ct);

        return data == null
            ? ApiResponse<HostInvitationResponse>.NotFoundResponse("Invitation not found.")
            : ApiResponse<HostInvitationResponse>.SuccessResponse(data);
    }

    public async Task<ApiResponse<HostInvitationResponse>> CreateAsync(
        CreateHostInvitationRequest request, int userId, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error != null) return ApiResponse<HostInvitationResponse>.ErrorResponse(error);

        var destinationId = await ResolveLocationIdAsync(request.DestinationId, ct);
        if (request.DestinationId.HasValue && destinationId == null)
            return ApiResponse<HostInvitationResponse>.NotFoundResponse("Destination location not found.");

        var org = await _unitOfWork.Organizations.GetByPublicIdAsync(request.HostOrganizationId.Value, ct);
        if (org == null)
            return ApiResponse<HostInvitationResponse>.NotFoundResponse("Host organization not found.");

        var entity = new HostInvitation
        {
            HostOrganizationId = org.Id,
            // Copied, not joined — see the entity's own note.
            HostOrganization = org.Name,
            HostEmail = request.HostEmail?.Trim(),
            MissionTitle = request.MissionTitle.Trim(),
            DestinationId = destinationId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            HeadcountCap = request.HeadcountCap,
            ResponseDeadline = request.ResponseDeadline,
            AttachmentUrl = request.AttachmentUrl?.Trim(),
            Notes = request.Notes?.Trim(),
            Status = HostInvitationStatuses.Logged,
        };
        entity.SetCreationAudit(userId);

        await _unitOfWork.HostInvitations.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(entity.PublicId, ct);
    }

    public async Task<ApiResponse<HostInvitationResponse>> UpdateAsync(
        Guid id, UpdateHostInvitationRequest request, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<HostInvitationResponse>.ErrorResponse("Invitation id is required.");

        var error = Validate(request);
        if (error != null) return ApiResponse<HostInvitationResponse>.ErrorResponse(error);

        var entity = await _unitOfWork.HostInvitations.Query().FirstOrDefaultAsync(i => i.PublicId == id, ct);
        if (entity == null)
            return ApiResponse<HostInvitationResponse>.NotFoundResponse("Invitation not found.");

        // Once converted the invitation is the mission's provenance — editing it
        // would silently disagree with the mission it produced.
        if (entity.Status == HostInvitationStatuses.Converted)
            return ApiResponse<HostInvitationResponse>.ConflictResponse(
                "This invitation has been converted into a mission and can no longer be edited.",
                "INVITATION_ALREADY_CONVERTED");

        var destinationId = await ResolveLocationIdAsync(request.DestinationId, ct);
        if (request.DestinationId.HasValue && destinationId == null)
            return ApiResponse<HostInvitationResponse>.NotFoundResponse("Destination location not found.");

        var org = await _unitOfWork.Organizations.GetByPublicIdAsync(request.HostOrganizationId.Value, ct);
        if (org == null)
            return ApiResponse<HostInvitationResponse>.NotFoundResponse("Host organization not found.");

        entity.HostOrganizationId = org.Id;
        entity.HostOrganization = org.Name;
        entity.HostEmail = request.HostEmail?.Trim();
        entity.MissionTitle = request.MissionTitle.Trim();
        entity.DestinationId = destinationId;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.HeadcountCap = request.HeadcountCap;
        entity.ResponseDeadline = request.ResponseDeadline;
        entity.AttachmentUrl = request.AttachmentUrl?.Trim();
        entity.Notes = request.Notes?.Trim();
        entity.SetUpdateAudit(userId);

        _unitOfWork.HostInvitations.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(entity.PublicId, ct);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<bool>.ErrorResponse("Invitation id is required.");

        var entity = await _unitOfWork.HostInvitations.Query().FirstOrDefaultAsync(i => i.PublicId == id, ct);
        if (entity == null)
            return ApiResponse<bool>.NotFoundResponse("Invitation not found.");

        if (entity.Status == HostInvitationStatuses.Converted)
            return ApiResponse<bool>.ConflictResponse(
                "This invitation has been converted into a mission and cannot be deleted.",
                "INVITATION_ALREADY_CONVERTED");

        entity.MarkAsDeleted(userId);
        _unitOfWork.HostInvitations.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Invitation deleted.");
    }

    public async Task<ApiResponse<HostInvitationResponse>> DeclineAsync(
        Guid id, string reason, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<HostInvitationResponse>.ErrorResponse("Invitation id is required.");

        var entity = await _unitOfWork.HostInvitations.Query().FirstOrDefaultAsync(i => i.PublicId == id, ct);
        if (entity == null)
            return ApiResponse<HostInvitationResponse>.NotFoundResponse("Invitation not found.");

        if (entity.Status == HostInvitationStatuses.Converted)
            return ApiResponse<HostInvitationResponse>.ConflictResponse(
                "This invitation has already been converted into a mission.", "INVITATION_ALREADY_CONVERTED");

        entity.Status = HostInvitationStatuses.Declined;
        // Appended, not overwritten — the log of why is worth keeping.
        if (!string.IsNullOrWhiteSpace(reason))
            entity.Notes = string.IsNullOrWhiteSpace(entity.Notes)
                ? reason.Trim()
                : $"{entity.Notes}\nDeclined: {reason.Trim()}";
        entity.SetUpdateAudit(userId);

        _unitOfWork.HostInvitations.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(entity.PublicId, ct);
    }

    public async Task<ApiResponse<EventResponse>> ConvertToMissionAsync(
        Guid id, ConvertInvitationRequest request, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<EventResponse>.ErrorResponse("Invitation id is required.");

        var invitation = await _unitOfWork.HostInvitations.Query()
            .Include(i => i.Destination)
            // Needed for the host's PublicId below — without it the mission would
            // be created with no host organisation at all.
            .Include(i => i.HostOrganizationRef)
            .FirstOrDefaultAsync(i => i.PublicId == id, ct);

        if (invitation == null)
            return ApiResponse<EventResponse>.NotFoundResponse("Invitation not found.");

        // One mission per invitation. Without this a double-click produces two
        // missions and the invitation only remembers the second.
        if (invitation.Status == HostInvitationStatuses.Converted)
            return ApiResponse<EventResponse>.ConflictResponse(
                "This invitation has already been converted into a mission.", "INVITATION_ALREADY_CONVERTED");

        if (invitation.Status == HostInvitationStatuses.Declined)
            return ApiResponse<EventResponse>.ConflictResponse(
                "This invitation was declined and cannot be converted.", "INVITATION_DECLINED");

        // Everything defaults from the invitation; the request only overrides.
        var startDate = request?.StartDate ?? invitation.StartDate;
        var endDate = request?.EndDate ?? invitation.EndDate;
        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
            return ApiResponse<EventResponse>.ErrorResponse("End date cannot be before the start date.");

        var destinationId = request?.DestinationId
            ?? (invitation.Destination != null ? invitation.Destination.PublicId : (Guid?)null);

        var createRequest = new CreateEventRequest
        {
            Title = string.IsNullOrWhiteSpace(request?.Title) ? invitation.MissionTitle : request.Title.Trim(),
            Type = request?.Type,
            VenueId = request?.VenueId,
            VenueName = request?.VenueName,
            StartDate = startDate,
            EndDate = endDate,
            Status = "planning",
            GuestModel = request?.GuestModel,
            DestinationId = destinationId,
            DelegationCap = request?.DelegationCap ?? invitation.HeadcountCap,
            // Only the conversion form can supply these — the host's letter says
            // nothing about which budget line we charge it to.
            DestinationTier = request?.DestinationTier,
            CostCenter = request?.CostCenter,
            FundingModel = request?.FundingModel,
            // The mission inherits the host FROM the invitation — that is the
            // provenance conversion exists to record.
            HostOrganizationId = invitation.HostOrganizationRef?.PublicId,
            HostEmail = invitation.HostEmail,
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var created = await _events.CreateEventAsync(createRequest, userId, ct);
            if (!created.Success)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return created;
            }

            var mission = await _unitOfWork.Events.Query()
                .FirstOrDefaultAsync(e => e.PublicId == created.Data.Id, ct);
            if (mission == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return ApiResponse<EventResponse>.ServerErrorResponse("The mission could not be read back after creation.");
            }

            // Link both ways so either side can be navigated without a lookup.
            mission.HostInvitationId = invitation.Id;
            _unitOfWork.Events.Update(mission);

            invitation.Status = HostInvitationStatuses.Converted;
            invitation.ConvertedEventId = mission.Id;
            invitation.SetUpdateAudit(userId);
            _unitOfWork.HostInvitations.Update(invitation);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync();

            created.Data.HostInvitationId = invitation.PublicId;
            return ApiResponse<EventResponse>.SuccessResponse(created.Data,
                "Mission created from invitation. Switch to it to start adding delegates.");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Converting invitation {InvitationId} to a mission failed", id);
            return ApiResponse<EventResponse>.ServerErrorResponse("The invitation could not be converted.");
        }
    }

    private static string Validate(CreateHostInvitationRequest r)
    {
        if (r == null) return "Request body is required.";
        if (!r.HostOrganizationId.HasValue || r.HostOrganizationId.Value == Guid.Empty)
            return "Host organization is required.";
        if (string.IsNullOrWhiteSpace(r.MissionTitle)) return "Mission title is required.";
        if (r.MissionTitle.Trim().Length > 300) return "Mission title must be 300 characters or fewer.";
        if (!string.IsNullOrWhiteSpace(r.HostEmail) && !r.HostEmail.Contains('@')) return "Host email is not a valid address.";
        if (r.HeadcountCap is < 1) return "Headcount cap must be at least 1.";

        if (r.StartDate.HasValue && r.EndDate.HasValue && r.EndDate < r.StartDate)
            return "End date cannot be before the start date.";

        // The deadline is when WE must answer, so it belongs before the trip.
        // One past the start date is a transcription error every time.
        if (r.ResponseDeadline.HasValue && r.StartDate.HasValue && r.ResponseDeadline > r.StartDate)
            return "The response deadline cannot fall after the mission starts.";
        if (r.ResponseDeadline.HasValue && r.EndDate.HasValue && !r.StartDate.HasValue
            && r.ResponseDeadline > r.EndDate)
            return "The response deadline cannot fall after the mission ends.";

        return null;
    }

    private async Task<int?> ResolveLocationIdAsync(Guid? publicId, CancellationToken ct)
    {
        if (!publicId.HasValue || publicId.Value == Guid.Empty) return null;
        var location = await _unitOfWork.Locations.GetByPublicIdAsync(publicId.Value, ct);
        return location?.Id;
    }

    private static System.Linq.Expressions.Expression<Func<HostInvitation, HostInvitationResponse>> Projection =>
        i => new HostInvitationResponse
        {
            Id = i.PublicId,
            HostOrganizationId = i.HostOrganizationRef != null ? (Guid?)i.HostOrganizationRef.PublicId : null,
            HostOrganization = i.HostOrganization,
            HostEmail = i.HostEmail,
            MissionTitle = i.MissionTitle,
            DestinationId = i.Destination != null ? (Guid?)i.Destination.PublicId : null,
            DestinationAddress = i.Destination != null ? i.Destination.Address : null,
            StartDate = i.StartDate,
            EndDate = i.EndDate,
            HeadcountCap = i.HeadcountCap,
            ResponseDeadline = i.ResponseDeadline,
            AttachmentUrl = i.AttachmentUrl,
            Status = i.Status,
            Notes = i.Notes,
            ConvertedEventId = i.ConvertedEvent != null ? (Guid?)i.ConvertedEvent.PublicId : null,
            ConvertedEventTitle = i.ConvertedEvent != null ? i.ConvertedEvent.Title : null,
            CreatedAt = i.CreatedAt,
        };
}
