using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Vehicle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VehicleEntity = DomainPersistence.Entities.Vehicle;

namespace Infrastructure.Services;

// Fleet vehicles. The type is a VehicleTypes lookup row, referenced by its
// public Guid at the API boundary and resolved to the internal int id here.
public class VehicleService(IUnitOfWork _unitOfWork, ILogger<VehicleService> _logger) : IVehicleService
{
    public async Task<ApiResponse<List<VehicleResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Vehicles.Query()
            .OrderBy(x => x.VehicleNumber)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<VehicleResponse>>.SuccessResponse(data);
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

            vehicle.VehicleTypeId = vehicleTypeId;
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

    // Shared projection. Must stay an expression tree (not a method) so EF can
    // translate it into the SELECT instead of pulling rows into memory.
    private static readonly Expression<Func<VehicleEntity, VehicleResponse>> Project = x => new VehicleResponse
    {
        Id = x.PublicId,
        VehicleTypeId = x.VehicleType.PublicId,
        VehicleTypeName = x.VehicleType.Name,
        FleetProviderId = x.FleetProvider == null ? (Guid?)null : x.FleetProvider.PublicId,
        FleetProviderName = x.FleetProvider == null ? null : x.FleetProvider.Name,
        VehicleModel = x.VehicleModel,
        VehicleNumber = x.VehicleNumber,
        VehicleImage = x.VehicleImage,
        Capacity = x.Capacity,
    };
}
