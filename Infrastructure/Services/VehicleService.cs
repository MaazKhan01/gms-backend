using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Vehicle;
using DomainPersistence.Entities;
using DomainPersistence.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VehicleEntity = DomainPersistence.Entities.Vehicle;

namespace Infrastructure.Services;

// Fleet vehicles. The type is a VehicleTypes lookup row, referenced by its
// public Guid at the API boundary and resolved to the internal int id here.
public class VehicleService(
    IUnitOfWork _unitOfWork,
    ITransportationConflictValidator _conflictValidator,
    ILogger<VehicleService> _logger) : IVehicleService
{
    public async Task<ApiResponse<List<VehicleResponse>>> GetAllAsync(
        Guid? eventId = null, VehicleUsageType? usageType = null, bool? unassigned = null,
        CancellationToken ct = default)
    {
        var query = ForEvent(_unitOfWork.Vehicles.Query(), eventId);

        if (usageType != null)
            query = query.Where(x => x.UsageType == usageType.Value);

        // The driver-invite dropdown asks for unassigned=true so a car already held
        // by another driver can't be picked. Only Fixed cars can be taken, so this
        // never hides an Open pool car from the transport screens.
        if (unassigned == true)
            query = query.Where(x => !x.DriverAssignments.Any());

        var data = await query
            .OrderBy(x => x.VehicleNumber)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<VehicleResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<VehicleResponse>>> GetAvailableAsync(
        DateTime from, DateTime? to, Guid? eventId = null, Guid? excludeTransportId = null, CancellationToken ct = default)
    {
        if (to != null && to <= from)
            return ApiResponse<List<VehicleResponse>>.ErrorResponse("The end of the window must be after its start");

        // The ride being edited holds its own vehicle — without this it would
        // report itself as busy and drop the current selection off the list.
        int? excludeId = null;
        if (excludeTransportId is { } transportId && transportId != Guid.Empty)
            excludeId = (await _unitOfWork.Transports.GetByPublicIdAsync(transportId, ct))?.Id;

        var busyIds = await _conflictValidator.GetBusyVehicleIdsAsync(from, to, excludeId, ct);

        var data = await ForEvent(_unitOfWork.Vehicles.Query(), eventId)
            // Open cars only. A Fixed car belongs to the one Open driver it is
            // assigned to and is filled in automatically when that driver accepts a
            // job, so offering it here would let an admin hand somebody else's
            // dedicated car to a different driver. Booking forms get the pool.
            .Where(x => x.UsageType == VehicleUsageType.Open)
            .Where(x => !busyIds.Contains(x.Id))
            .OrderBy(x => x.VehicleNumber)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<VehicleResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<VehicleBookingRow>>> GetBookingsAsync(
        Guid? eventId = null, DateTime? from = null, DateTime? to = null,
        Guid? vehicleId = null, Guid? driverId = null, CancellationToken ct = default)
    {
        // Cancelled rides are dropped: this screen answers "when is this car
        // taken", and a cancelled slot isn't. Completed ones stay — they're
        // history the dispatcher still needs to see.
        var query = _unitOfWork.Transports.Query()
            .Where(t => t.VehicleId != null && t.TripStatus != TransportStatuses.Cancelled);

        if (eventId is { } evId && evId != Guid.Empty)
            query = query.Where(t => t.EventGuest.Event.PublicId == evId);
        if (from != null)
            query = query.Where(t => t.PickupTime >= from);
        if (to != null)
            query = query.Where(t => t.PickupTime < to);
        if (vehicleId is { } vId && vId != Guid.Empty)
            query = query.Where(t => t.Vehicle.PublicId == vId);
        if (driverId is { } dId && dId != Guid.Empty)
            query = query.Where(t => t.Driver.PublicId == dId);

        var data = await query
            .OrderBy(t => t.Vehicle.VehicleNumber).ThenBy(t => t.PickupTime)
            .Select(ProjectBooking)
            .ToListAsync(ct);

        return ApiResponse<List<VehicleBookingRow>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<VehicleResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var vehicle = await _unitOfWork.Vehicles.Query()
            .Where(x => x.PublicId == id)
            .Select(Project)
            .FirstOrDefaultAsync(ct);

        return vehicle == null
            ? ApiResponse<VehicleResponse>.NotFoundResponse("Vehicle not found")
            : ApiResponse<VehicleResponse>.SuccessResponse(vehicle);
    }

    public async Task<ApiResponse<VehicleResponse>> CreateAsync(
        CreateVehicleRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var (error, vehicleTypeId, fleetProviderId) = await ValidateAsync(request, null, ct);
            if (error != null)
                return ApiResponse<VehicleResponse>.ErrorResponse(error);

            var vehicle = new VehicleEntity
            {
                VehicleTypeId = vehicleTypeId,
                UsageType = request.UsageType,
                FleetProviderId = fleetProviderId,
                VehicleModel = request.VehicleModel.Trim(),
                VehicleNumber = request.VehicleNumber.Trim(),
                VehicleImage = string.IsNullOrWhiteSpace(request.VehicleImage) ? null : request.VehicleImage.Trim(),
                Capacity = request.Capacity,
            };

            vehicle.SetCreationAudit(userId);
            await _unitOfWork.Vehicles.AddAsync(vehicle, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(vehicle.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle");
            return ApiResponse<VehicleResponse>.ServerErrorResponse("An error occurred while creating the vehicle");
        }
    }

    public async Task<ApiResponse<VehicleResponse>> UpdateAsync(
        Guid id, UpdateVehicleRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var vehicle = await _unitOfWork.Vehicles.GetByPublicIdAsync(id, ct);
            if (vehicle == null)
                return ApiResponse<VehicleResponse>.NotFoundResponse("Vehicle not found");

            var (error, vehicleTypeId, fleetProviderId) = await ValidateAsync(request, vehicle.Id, ct);
            if (error != null)
                return ApiResponse<VehicleResponse>.ErrorResponse(error);

            // Switching a Fixed car to Open would leave its dedicated driver holding a
            // pool car, so the assignment has to be released first — deliberately a
            // refusal rather than a silent unassign.
            if (vehicle.UsageType == VehicleUsageType.Fixed && request.UsageType != VehicleUsageType.Fixed)
            {
                var held = await _unitOfWork.DriverProfiles.Query()
                    .AnyAsync(d => d.AssignedVehicleId == vehicle.Id, ct);
                if (held)
                    return ApiResponse<VehicleResponse>.ErrorResponse(
                        "This vehicle is assigned to a driver. Remove the assignment before changing its usage type.");
            }

            vehicle.VehicleTypeId = vehicleTypeId;
            vehicle.UsageType = request.UsageType;
            vehicle.FleetProviderId = fleetProviderId;
            vehicle.VehicleModel = request.VehicleModel.Trim();
            vehicle.VehicleNumber = request.VehicleNumber.Trim();
            vehicle.VehicleImage = string.IsNullOrWhiteSpace(request.VehicleImage) ? null : request.VehicleImage.Trim();
            vehicle.Capacity = request.Capacity;

            vehicle.SetUpdateAudit(userId);
            _unitOfWork.Vehicles.Update(vehicle);
            await _unitOfWork.SaveChangesAsync(ct);

            return await GetByIdAsync(vehicle.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle {VehicleId}", id);
            return ApiResponse<VehicleResponse>.ServerErrorResponse("An error occurred while updating the vehicle");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        try
        {
            var vehicle = await _unitOfWork.Vehicles.GetByPublicIdAsync(id, ct);
            if (vehicle == null)
                return ApiResponse<bool>.NotFoundResponse("Vehicle not found");

            vehicle.MarkAsDeleted(userId);
            _unitOfWork.Vehicles.Update(vehicle);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Vehicle deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle {VehicleId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the vehicle");
        }
    }

    // Returns (error message, resolved internal VehicleTypeId, resolved internal
    // FleetProviderId). Error is null when the request is valid.
    private async Task<(string Error, int VehicleTypeId, int? FleetProviderId)> ValidateAsync(
        CreateVehicleRequest request, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.VehicleModel))
            return ("Vehicle model is required", 0, null);
        if (string.IsNullOrWhiteSpace(request.VehicleNumber))
            return ("Vehicle number is required", 0, null);
        if (request.Capacity is <= 0)
            return ("Capacity must be greater than zero", 0, null);
        // Required going forward. Pre-existing rows keep whatever the backfill gave
        // them; the column stays nullable only for their sake.
        if (request.UsageType == null)
            return ("Usage type is required", 0, null);
        if (!Enum.IsDefined(request.UsageType.Value))
            return ("Usage type is not valid", 0, null);

        var vehicleType = await _unitOfWork.VehicleTypes.Query()
            .FirstOrDefaultAsync(x => x.PublicId == request.VehicleTypeId, ct);
        if (vehicleType == null)
            return ("Vehicle type not found", 0, null);

        // Optional — absent/empty means the vehicle isn't sourced from a provider.
        int? fleetProviderId = null;
        if (request.FleetProviderId is { } providerPublicId && providerPublicId != Guid.Empty)
        {
            var provider = await _unitOfWork.FleetProviders.Query()
                .FirstOrDefaultAsync(x => x.PublicId == providerPublicId, ct);
            if (provider == null)
                return ("Fleet provider not found", 0, null);
            fleetProviderId = provider.Id;
        }

        var number = request.VehicleNumber.Trim();
        var duplicate = await _unitOfWork.Vehicles.Query()
            .AnyAsync(x => x.VehicleNumber == number && (!excludeId.HasValue || x.Id != excludeId.Value), ct);
        if (duplicate)
            return ("A vehicle with this number already exists", 0, null);

        return (null, vehicleType.Id, fleetProviderId);
    }

    // Vehicles reach an event through their provider — no second EventId column
    // to keep in sync. An in-house car (no provider) serves every event.
    private static IQueryable<VehicleEntity> ForEvent(IQueryable<VehicleEntity> query, Guid? eventId)
        => eventId == null || eventId == Guid.Empty
            ? query
            : query.Where(x => x.FleetProviderId == null || x.FleetProvider.Event.PublicId == eventId);

    // Shared projection. Must stay an expression tree (not a method) so EF can
    // translate it into the SELECT instead of pulling rows into memory.
    private static readonly Expression<Func<VehicleEntity, VehicleResponse>> Project = x => new VehicleResponse
    {
        Id = x.PublicId,
        VehicleTypeId = x.VehicleType.PublicId,
        VehicleTypeName = x.VehicleType.Name,
        FleetProviderId = x.FleetProvider == null ? (Guid?)null : x.FleetProvider.PublicId,
        FleetProviderName = x.FleetProvider == null ? null : x.FleetProvider.Name,
        EventId = x.FleetProvider == null ? (Guid?)null : x.FleetProvider.Event.PublicId,
        UsageType = x.UsageType,
        UsageTypeName = x.UsageType == null ? null : x.UsageType.ToString(),
        // Correlated EXISTS, not a join — a vehicle has at most one holder and the
        // list must not fan out into duplicate rows.
        IsAssignedToDriver = x.DriverAssignments.Any(),
        VehicleModel = x.VehicleModel,
        VehicleNumber = x.VehicleNumber,
        VehicleImage = x.VehicleImage,
        Capacity = x.Capacity,
    };

    private static readonly Expression<Func<Transport, VehicleBookingRow>> ProjectBooking = t => new VehicleBookingRow
    {
        Id = t.PublicId,
        VehicleId = t.Vehicle.PublicId,
        VehicleNumber = t.Vehicle.VehicleNumber,
        VehicleModel = t.Vehicle.VehicleModel,
        VehicleTypeName = t.Vehicle.VehicleType.Name,
        VehicleImage = t.Vehicle.VehicleImage,
        FleetProviderName = t.Vehicle.FleetProvider == null ? null : t.Vehicle.FleetProvider.Name,
        DriverId = t.Driver == null ? (Guid?)null : t.Driver.PublicId,
        DriverName = t.Driver == null ? null : (t.Driver.User.FirstName + " " + t.Driver.User.LastName).Trim(),
        DriverPhone = t.Driver == null ? null : t.Driver.User.Phone,
        EventGuestId = t.EventGuest.PublicId,
        GuestName = (t.EventGuest.Guest.FirstName + " " + t.EventGuest.Guest.LastName).Trim(),
        GuestEmail = t.EventGuest.Guest.Email,
        GuestPhotoUrl = t.EventGuest.Guest.PhotoUrl,
        PickupTime = t.PickupTime,
        DropoffTime = t.DropoffTime,
        Pickup = t.PickupLocation == null ? null : t.PickupLocation.Address,
        Dropoff = t.DropoffLocation == null ? null : t.DropoffLocation.Address,
        Status = t.TripStatus,
        RideSource = t.RideSource,
    };
}
