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
            // A ride belongs to a participation, which is what carries the event.
            var participation = await LoadParticipationAsync(request.EventGuestId, ct);
            if (participation == null) return ApiResponse<ScheduleRow>.NotFoundResponse("Guest not found");

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

            var guestConflict = await _conflictValidator.CheckGuestConflictAsync(participation.Id, request.ScheduledTime, ct: ct);
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
                EventGuestId = participation.Id,
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
                    transport.Tokens(participation), ct);

            return await GetScheduleRowAsync(transport.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transportation schedule for participation {EventGuestId}", request.EventGuestId);
            return ApiResponse<ScheduleRow>.ServerErrorResponse("An error occurred while creating the schedule");
        }
    }

    public async Task<ApiResponse<List<ScheduleRow>>> GetGuestScheduleAsync(Guid eventGuestId, CancellationToken ct = default)
    {
        var participation = await _unitOfWork.EventGuests.GetByPublicIdAsync(eventGuestId, ct);
        if (participation == null) return ApiResponse<List<ScheduleRow>>.NotFoundResponse("Guest not found");

        var data = await _unitOfWork.Transports.Query()
            .Where(t => t.EventGuestId == participation.Id)
            .OrderBy(t => t.PickupTime == null).ThenBy(t => t.PickupTime)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<ScheduleRow>>.SuccessResponse(data);
    }

    /// <summary>The drivers already on this participation's rides. Transport is the
    /// only record of a driver-to-guest assignment (DriverId + EventGuestId), so the
    /// distinct drivers across the participation's trips ARE the pool.</summary>
    public async Task<ApiResponse<List<AssignedDriverDto>>> GetAssignedDriversAsync(Guid eventGuestId, CancellationToken ct = default)
    {
        var participation = await _unitOfWork.EventGuests.GetByPublicIdAsync(eventGuestId, ct);
        if (participation == null) return ApiResponse<List<AssignedDriverDto>>.NotFoundResponse("Guest not found");

        // Distinct on the raw columns, then shaped client-side — Distinct over a
        // projected DTO isn't reliably translatable. Trim client-side for the same
        // reason it's cheap: the rows are already in memory.
        var rows = await _unitOfWork.Transports.QueryNoTracking()
            .Where(t => t.EventGuestId == participation.Id && t.Driver != null)
            .Select(t => new { t.Driver.PublicId, t.Driver.User.FirstName, t.Driver.User.LastName })
            .Distinct()
            .ToListAsync(ct);

        var data = rows
            .Select(r => new AssignedDriverDto
            {
                DriverId = r.PublicId,
                DriverName = (r.FirstName + " " + r.LastName).Trim(),
            })
            .OrderBy(d => d.DriverName)
            .ToList();

        return ApiResponse<List<AssignedDriverDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<PaginatedResponse<ScheduleRow>>> GetEventScheduleAsync(
        Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<PaginatedResponse<ScheduleRow>>.NotFoundResponse("Event not found");

        // Event filtering goes through the participation — that is the only place
        // a ride's event is recorded.
        var query = _unitOfWork.Transports.Query().Where(t => t.EventGuest.EventId == ev.Id);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(t =>
                (t.EventGuest.Guest.FirstName + " " + t.EventGuest.Guest.LastName).Contains(term) ||
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
            // transport.EventGuestId is a PARTICIPATION id; SendToGuestAsync wants the
            // PERSON id it can resolve to a User and their devices. Resolve once here
            // and reuse for both notifications rather than passing the wrong int.
            var participation = await LoadParticipationAsync(transport.EventGuestId, ct);

            if (participation != null)
                await _notifications.SendToGuestAsync(participation.GuestId,
                    NotificationTemplates.TransportGuestRideCancelled,
                    new Dictionary<string, string>
                    {
                        ["transportId"] = transport.PublicId.ToString(),
                        ["eventGuestId"] = participation.PublicId.ToString(),
                        ["eventId"] = participation.Event?.PublicId.ToString() ?? string.Empty,
                    }, ct);

            if (transport.DriverId.HasValue)
            {
                await _notifications.SendToDriverAsync(_unitOfWork, transport.DriverId.Value,
                    NotificationTemplates.TransportDriverTripCancelled,
                    transport.Tokens(participation), ct);
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

    // Guest + Event eagerly loaded: both notification paths need the person id
    // and the event, and neither is reachable from a bare Transport row.
    private Task<EventGuest> LoadParticipationAsync(int eventGuestId, CancellationToken ct)
        => _unitOfWork.EventGuests.Query()
            .Include(eg => eg.Guest)
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.Id == eventGuestId, ct);

    private Task<EventGuest> LoadParticipationAsync(Guid eventGuestPublicId, CancellationToken ct)
        => _unitOfWork.EventGuests.Query()
            .Include(eg => eg.Guest)
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.PublicId == eventGuestPublicId, ct);

    private static async Task<int?> ResolveNullableId<T>(IGenericRepository<T> repo, Guid? publicId, CancellationToken ct) where T : Entity
        => publicId == null || publicId == Guid.Empty ? null : (await repo.GetByPublicIdAsync(publicId.Value, ct))?.Id;

    private static readonly System.Linq.Expressions.Expression<Func<Transport, ScheduleRow>> Project = t => new ScheduleRow
    {
        Id = t.PublicId,
        EventGuestId = t.EventGuest.PublicId,
        PersonId = t.EventGuest.Guest.PublicId,
        EventId = t.EventGuest.Event.PublicId,
        GuestName = (t.EventGuest.Guest.FirstName + " " + t.EventGuest.Guest.LastName).Trim(),
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
