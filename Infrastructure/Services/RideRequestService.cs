using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Transportation;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class RideRequestService(
    IUnitOfWork _unitOfWork,
    ITransportationConflictValidator _conflictValidator,
    IRealTimeAlertService _realTimeAlerts,
    ILogger<RideRequestService> _logger) : IRideRequestService
{
    public async Task<ApiResponse<RideRequestRow>> CreateAsync(int guestId, CreateRideRequestRequest request, CancellationToken ct = default)
    {
        try
        {
            var candidateTime = request.RequestedTime ?? DateTime.UtcNow;
            var conflict = await _conflictValidator.CheckGuestConflictAsync(guestId, candidateTime, ct: ct);
            if (conflict.HasConflict)
                return ApiResponse<RideRequestRow>.ConflictResponse(conflict.Message, "TRANSPORTATION_CONFLICT");

            var rideRequest = new RideRequest
            {
                GuestId = guestId,
                PickupLocationId = await ResolveNullableId(_unitOfWork.Locations, request.PickupLocationId, ct),
                DropoffLocationId = await ResolveNullableId(_unitOfWork.Locations, request.DropoffLocationId, ct),
                RequestedTime = request.RequestedTime,
                Notes = request.Notes?.Trim(),
                Status = RideRequestStatuses.Open,
            };
            // No SetCreationAudit — guest-authored rows leave CreatedBy null, same
            // convention as SupportMessage.FromGuest rows (CreatedAt uses the DB default).
            await _unitOfWork.RideRequests.AddAsync(rideRequest, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await NotifyAvailableDriversAsync(ct);

            return await GetRowAsync(rideRequest.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ride request for guest {GuestId}", guestId);
            return ApiResponse<RideRequestRow>.ServerErrorResponse("An error occurred while creating the ride request");
        }
    }

    public async Task<ApiResponse<List<RideRequestRow>>> GetMineAsync(int guestId, CancellationToken ct = default)
    {
        var data = await _unitOfWork.RideRequests.Query()
            .Where(r => r.GuestId == guestId)
            .OrderByDescending(r => r.Id)
            .Select(Project)
            .ToListAsync(ct);
        return ApiResponse<List<RideRequestRow>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<bool>> CancelAsync(Guid rideRequestId, int? guestId, CancellationToken ct = default)
    {
        var request = await _unitOfWork.RideRequests.Query()
            .FirstOrDefaultAsync(r => r.PublicId == rideRequestId, ct);
        if (request == null) return ApiResponse<bool>.NotFoundResponse("Ride request not found");
        if (guestId.HasValue && request.GuestId != guestId.Value)
            return ApiResponse<bool>.ForbiddenResponse("This ride request does not belong to you");
        if (request.Status != RideRequestStatuses.Open)
            return ApiResponse<bool>.ErrorResponse($"A request that is '{request.Status}' cannot be cancelled");

        request.Status = RideRequestStatuses.Cancelled;
        _unitOfWork.RideRequests.Update(request);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Ride request cancelled");
    }

    public async Task<ApiResponse<List<RideRequestRow>>> GetOpenAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.RideRequests.Query()
            .Where(r => r.Status == RideRequestStatuses.Open)
            .OrderBy(r => r.RequestedTime == null).ThenBy(r => r.RequestedTime)
            .Select(Project)
            .ToListAsync(ct);
        return ApiResponse<List<RideRequestRow>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<RideRequestRow>> AcceptAsync(int driverId, Guid rideRequestId, CancellationToken ct = default)
    {
        try
        {
            var request = await _unitOfWork.RideRequests.Query()
                .FirstOrDefaultAsync(r => r.PublicId == rideRequestId && r.Status == RideRequestStatuses.Open, ct);
            if (request == null)
                return ApiResponse<RideRequestRow>.ConflictResponse("This ride request is no longer available.", "TRANSPORTATION_CONFLICT");

            var candidateTime = request.RequestedTime ?? DateTime.UtcNow;
            var driverConflict = await _conflictValidator.CheckDriverConflictAsync(driverId, candidateTime, ct: ct);
            if (driverConflict.HasConflict)
                return ApiResponse<RideRequestRow>.ConflictResponse(driverConflict.Message, "TRANSPORTATION_CONFLICT");

            // First driver to commit wins — RideRequest.RowVersion is the EF
            // concurrency token, so a second driver's SaveChangesAsync (still
            // holding the pre-update RowVersion) throws below instead of both
            // successfully accepting the same request.
            request.Status = RideRequestStatuses.Accepted;
            request.AcceptedByDriverId = driverId;
            request.AcceptedAt = DateTime.UtcNow;
            _unitOfWork.RideRequests.Update(request);

            var transport = new Transport
            {
                GuestId = request.GuestId,
                DriverId = driverId,
                PickupLocationId = request.PickupLocationId,
                DropoffLocationId = request.DropoffLocationId,
                PickupTime = candidateTime,
                Notes = request.Notes,
                RideSource = "on-demand",
                RideRequestId = request.Id,
                TripStatus = TransportStatuses.Assigned,
            };
            await _unitOfWork.Transports.AddAsync(transport, ct);

            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiResponse<RideRequestRow>.ConflictResponse(
                    "Another driver already accepted this request.", "TRANSPORTATION_CONFLICT");
            }

            var history = new TransportStatusHistory { TransportId = transport.Id, Status = TransportStatuses.Assigned };
            await _unitOfWork.TransportStatusHistories.AddAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _realTimeAlerts.SendToGroupAsync(RealtimeTopics.TransportationRequestAccepted, $"guest:{request.GuestId}",
                "Driver assigned", "A driver has accepted your ride request.");
            await _realTimeAlerts.SendToGroupAsync(RealtimeTopics.TransportationDriverAssigned, $"guest:{request.GuestId}",
                "Driver assigned", "A driver has accepted your ride request.");

            return await GetRowAsync(request.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting ride request {RideRequestId}", rideRequestId);
            return ApiResponse<RideRequestRow>.ServerErrorResponse("An error occurred while accepting the ride request");
        }
    }

    private async Task NotifyAvailableDriversAsync(CancellationToken ct)
    {
        // Per-user sends rather than a shared SignalR group — avoids the
        // reconnect/rejoin edge case a connection-scoped group would have.
        var driverUserIds = await _unitOfWork.DriverProfiles.Query()
            .Where(d => d.IsAvailable)
            .Select(d => d.UserId)
            .ToListAsync(ct);

        foreach (var userId in driverUserIds)
        {
            await _realTimeAlerts.SendToUserAsync(RealtimeTopics.TransportationRequestAvailable, userId.ToString(),
                "New ride request", "A guest has requested transportation.");
        }
    }

    private async Task<ApiResponse<RideRequestRow>> GetRowAsync(int id, CancellationToken ct)
    {
        var row = await _unitOfWork.RideRequests.Query()
            .Where(r => r.Id == id)
            .Select(Project)
            .FirstOrDefaultAsync(ct);
        return ApiResponse<RideRequestRow>.SuccessResponse(row);
    }

    private static async Task<int?> ResolveNullableId<T>(IGenericRepository<T> repo, Guid? publicId, CancellationToken ct) where T : Entity
        => publicId == null || publicId == Guid.Empty ? null : (await repo.GetByPublicIdAsync(publicId.Value, ct))?.Id;

    private static readonly System.Linq.Expressions.Expression<Func<RideRequest, RideRequestRow>> Project = r => new RideRequestRow
    {
        Id = r.PublicId,
        GuestId = r.Guest.PublicId,
        GuestName = (r.Guest.FirstName + " " + r.Guest.LastName).Trim(),
        Pickup = r.PickupLocation == null ? null : r.PickupLocation.Address,
        Dropoff = r.DropoffLocation == null ? null : r.DropoffLocation.Address,
        RequestedTime = r.RequestedTime,
        Status = r.Status,
        AcceptedByDriverId = r.AcceptedByDriver == null ? null : (Guid?)r.AcceptedByDriver.PublicId,
        AcceptedByDriverName = r.AcceptedByDriver == null ? null : (r.AcceptedByDriver.User.FirstName + " " + r.AcceptedByDriver.User.LastName).Trim(),
        AcceptedAt = r.AcceptedAt,
        TransportId = r.Transport == null ? null : (Guid?)r.Transport.PublicId,
    };
}
