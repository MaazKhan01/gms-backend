using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Constants.Notification;
using Core.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Transportation;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class TransportationScheduleService(
    IUnitOfWork _unitOfWork,
    ITransportationConflictValidator _conflictValidator,
    INotificationManagerService _notifications,
    ILogger<TransportationScheduleService> _logger) : ITransportationScheduleService
{
    public async Task<ApiResponse<ScheduleRow>> CreateScheduleAsync(CreateScheduleRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var guest = await _unitOfWork.Guests.GetByPublicIdAsync(request.GuestId, ct);
            if (guest == null) return ApiResponse<ScheduleRow>.NotFoundResponse("Guest not found");

            if (request.DropoffTime == null)
                return ApiResponse<ScheduleRow>.ErrorResponse("Dropoff time is required");
            if (request.DropoffTime <= request.ScheduledTime)
                return ApiResponse<ScheduleRow>.ErrorResponse("Dropoff time must be after the pickup time");

            int? driverId = null;
            if (request.DriverId is { } dId && dId != Guid.Empty)
            {
                var driver = await _unitOfWork.DriverProfiles.GetByPublicIdAsync(dId, ct);
                if (driver == null) return ApiResponse<ScheduleRow>.ErrorResponse("Invalid driver");
                driverId = driver.Id;
            }

            var vehicleId = await ResolveNullableId(_unitOfWork.Vehicles, request.VehicleId, ct);

            var guestConflict = await _conflictValidator.CheckGuestConflictAsync(guest.Id, request.ScheduledTime, ct: ct);
            if (guestConflict.HasConflict)
                return ApiResponse<ScheduleRow>.ConflictResponse(guestConflict.Message, "TRANSPORTATION_CONFLICT");

            if (driverId.HasValue)
            {
                var driverConflict = await _conflictValidator.CheckDriverConflictAsync(driverId.Value, request.ScheduledTime, ct: ct);
                if (driverConflict.HasConflict)
                    return ApiResponse<ScheduleRow>.ConflictResponse(driverConflict.Message, "TRANSPORTATION_CONFLICT");
            }

            if (vehicleId.HasValue)
            {
                var vehicleConflict = await _conflictValidator.CheckVehicleConflictAsync(
                    vehicleId.Value, request.ScheduledTime, request.DropoffTime, ct: ct);
                if (vehicleConflict.HasConflict)
                    return ApiResponse<ScheduleRow>.ConflictResponse(vehicleConflict.Message, "TRANSPORTATION_CONFLICT");
            }

            var transport = new Transport
            {
                GuestId = guest.Id,
                DriverId = driverId,
                VehicleId = vehicleId,
                PickupLocationId = await ResolveNullableId(_unitOfWork.Locations, request.PickupLocationId, ct),
                DropoffLocationId = await ResolveNullableId(_unitOfWork.Locations, request.DropoffLocationId, ct),
                PickupTime = request.ScheduledTime,
                DropoffTime = request.DropoffTime,
                Notes = request.Notes?.Trim(),
                RideSource = "scheduled",
                TripStatus = driverId.HasValue ? TransportStatuses.Assigned : TransportStatuses.Pending,
            };
            transport.SetCreationAudit(userId);
            await _unitOfWork.Transports.AddAsync(transport, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await RecordStatusHistoryAsync(transport.Id, transport.TripStatus, userId, ct);

            if (driverId.HasValue)
                await _notifications.SendToDriverAsync(_unitOfWork, driverId.Value,
                    NotificationTemplates.TransportDriverAssigned,
                    transport.Tokens(guest), ct);

            return await GetScheduleRowAsync(transport.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transportation schedule for guest {GuestId}", request.GuestId);
            return ApiResponse<ScheduleRow>.ServerErrorResponse("An error occurred while creating the schedule");
        }
    }

    public async Task<ApiResponse<List<AssignedDriverDto>>> AssignDriversToGuestAsync(
        Guid guestId, AssignDriversRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var guest = await _unitOfWork.Guests.GetByPublicIdAsync(guestId, ct);
            if (guest == null) return ApiResponse<List<AssignedDriverDto>>.NotFoundResponse("Guest not found");

            var driverIds = new List<int>();
            foreach (var publicId in request.DriverIds ?? new List<Guid>())
            {
                var driver = await _unitOfWork.DriverProfiles.GetByPublicIdAsync(publicId, ct);
                if (driver == null) return ApiResponse<List<AssignedDriverDto>>.ErrorResponse($"Invalid driver: {publicId}");
                driverIds.Add(driver.Id);
            }

            var existing = await _unitOfWork.GuestDriverAssignments.Query()
                .Where(a => a.GuestId == guest.Id)
                .Select(a => a.DriverId)
                .ToListAsync(ct);

            foreach (var driverId in driverIds.Except(existing))
            {
                var assignment = new GuestDriverAssignment { GuestId = guest.Id, DriverId = driverId };
                assignment.SetCreationAudit(userId);
                await _unitOfWork.GuestDriverAssignments.AddAsync(assignment, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetAssignedDriversAsync(guestId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning drivers to guest {GuestId}", guestId);
            return ApiResponse<List<AssignedDriverDto>>.ServerErrorResponse("An error occurred while assigning drivers");
        }
    }

    public async Task<ApiResponse<List<AssignedDriverDto>>> GetAssignedDriversAsync(Guid guestId, CancellationToken ct = default)
    {
        var data = await _unitOfWork.GuestDriverAssignments.Query()
            .Where(a => a.Guest.PublicId == guestId)
            .Select(a => new AssignedDriverDto
            {
                DriverId = a.Driver.PublicId,
                DriverName = (a.Driver.User.FirstName + " " + a.Driver.User.LastName).Trim(),
            })
            .ToListAsync(ct);
        return ApiResponse<List<AssignedDriverDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<ScheduleRow>>> GetGuestScheduleAsync(Guid guestId, CancellationToken ct = default)
    {
        var guest = await _unitOfWork.Guests.GetByPublicIdAsync(guestId, ct);
        if (guest == null) return ApiResponse<List<ScheduleRow>>.NotFoundResponse("Guest not found");

        var data = await _unitOfWork.Transports.Query()
            .Where(t => t.GuestId == guest.Id)
            .OrderBy(t => t.PickupTime == null).ThenBy(t => t.PickupTime)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<ScheduleRow>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<PaginatedResponse<ScheduleRow>>> GetEventScheduleAsync(
        Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<PaginatedResponse<ScheduleRow>>.NotFoundResponse("Event not found");

        var query = _unitOfWork.Transports.Query().Where(t => t.Guest.EventId == ev.Id);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(t =>
                (t.Guest.FirstName + " " + t.Guest.LastName).Contains(term) ||
                (t.Driver != null && (t.Driver.User.FirstName + " " + t.Driver.User.LastName).Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var data = await query
            .OrderBy(t => t.PickupTime == null).ThenBy(t => t.PickupTime)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<PaginatedResponse<ScheduleRow>>.SuccessResponse(
            new PaginatedResponse<ScheduleRow>(data, total, request.PageNumber, request.PageSize));
    }

    public async Task<ApiResponse<bool>> CancelScheduleAsync(Guid transportId, int userId, CancellationToken ct = default)
    {
        try
        {
            var transport = await _unitOfWork.Transports.Query()
                .FirstOrDefaultAsync(t => t.PublicId == transportId, ct);
            if (transport == null) return ApiResponse<bool>.NotFoundResponse("Schedule not found");

            if (transport.TripStatus == TransportStatuses.Cancelled)
                return ApiResponse<bool>.SuccessResponse(true, "Schedule already cancelled");
            if (transport.TripStatus == TransportStatuses.Completed)
                return ApiResponse<bool>.ErrorResponse("A completed ride cannot be cancelled");

            transport.TripStatus = TransportStatuses.Cancelled;
            transport.SetUpdateAudit(userId);
            _unitOfWork.Transports.Update(transport);
            await _unitOfWork.SaveChangesAsync(ct);

            await RecordStatusHistoryAsync(transport.Id, TransportStatuses.Cancelled, userId, ct);

            // Same reason as the driver notification just below — through the
            // manager so it persists and reaches the VIP app over FCM, not just
            // whatever portal tab happens to be connected.
            await _notifications.SendToGuestAsync(transport.GuestId,
                NotificationTemplates.TransportGuestRideCancelled,
                new Dictionary<string, string> { ["transportId"] = transport.PublicId.ToString() }, ct);

            if (transport.DriverId.HasValue)
            {
                var guest = await _unitOfWork.Guests.Query()
                    .FirstOrDefaultAsync(g => g.Id == transport.GuestId, ct);
                await _notifications.SendToDriverAsync(_unitOfWork, transport.DriverId.Value,
                    NotificationTemplates.TransportDriverTripCancelled,
                    transport.Tokens(guest), ct);
            }

            return ApiResponse<bool>.SuccessResponse(true, "Schedule cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling transportation schedule {TransportId}", transportId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while cancelling the schedule");
        }
    }

    private async Task<ApiResponse<ScheduleRow>> GetScheduleRowAsync(int transportId, CancellationToken ct)
    {
        var row = await _unitOfWork.Transports.Query()
            .Where(t => t.Id == transportId)
            .Select(Project)
            .FirstOrDefaultAsync(ct);
        return ApiResponse<ScheduleRow>.SuccessResponse(row, "Schedule created");
    }

    private async Task RecordStatusHistoryAsync(int transportId, string status, int? userId, CancellationToken ct)
    {
        var history = new TransportStatusHistory { TransportId = transportId, Status = status, ChangedByUserId = userId };
        history.SetCreationAudit(userId ?? 0);
        await _unitOfWork.TransportStatusHistories.AddAsync(history, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static async Task<int?> ResolveNullableId<T>(IGenericRepository<T> repo, Guid? publicId, CancellationToken ct) where T : Entity
        => publicId == null || publicId == Guid.Empty ? null : (await repo.GetByPublicIdAsync(publicId.Value, ct))?.Id;

    private static readonly System.Linq.Expressions.Expression<Func<Transport, ScheduleRow>> Project = t => new ScheduleRow
    {
        Id = t.PublicId,
        GuestId = t.Guest.PublicId,
        GuestName = (t.Guest.FirstName + " " + t.Guest.LastName).Trim(),
        DriverId = t.Driver == null ? null : (Guid?)t.Driver.PublicId,
        DriverName = t.Driver == null ? null : (t.Driver.User.FirstName + " " + t.Driver.User.LastName).Trim(),
        VehicleId = t.Vehicle == null ? null : (Guid?)t.Vehicle.PublicId,
        Vehicle = t.Vehicle == null ? null : (t.Vehicle.VehicleNumber + " · " + t.Vehicle.VehicleModel),
        Pickup = t.PickupLocation == null ? null : t.PickupLocation.Address,
        Dropoff = t.DropoffLocation == null ? null : t.DropoffLocation.Address,
        ScheduledTime = t.PickupTime,
        DropoffTime = t.DropoffTime,
        ActualPickupTime = t.ActualPickupTime,
        ActualDropOffTime = t.ActualDropOffTime,
        Status = t.TripStatus,
        RideSource = t.RideSource,
        Notes = t.Notes,
        TotalFare = t.TotalFare,
        Currency = t.Currency,
    };
}
