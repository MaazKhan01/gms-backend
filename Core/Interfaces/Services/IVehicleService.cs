using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Vehicle;
using DomainPersistence.Enums;

namespace Core.Interfaces.Services;

public interface IVehicleService
{
    /// <summary>Pass eventId to keep the list to vehicles the event can actually
    /// use: its own providers' cars plus in-house ones. Omit it for the whole fleet.
    /// usageType narrows to Fixed or Open cars; unassigned=true drops any car a
    /// driver already holds — together they feed the driver-invite vehicle picker.</summary>
    Task<ApiResponse<List<VehicleResponse>>> GetAllAsync(
        Guid? eventId = null, VehicleUsageType? usageType = null, bool? unassigned = null,
        CancellationToken ct = default);

    /// <summary>Open (pool) vehicles minus anything already booked over [from, to) —
    /// what a booking form's vehicle dropdown should show. Fixed vehicles are left
    /// out on purpose: each belongs to the one open driver it is assigned to and is
    /// attached to a trip automatically when that driver accepts it. `to` may be null
    /// (the policy's default ride duration stands in); pass the transport being edited
    /// as excludeTransportId so its own vehicle stays selectable.</summary>
    Task<ApiResponse<List<VehicleResponse>>> GetAvailableAsync(
        DateTime from, DateTime? to, Guid? eventId = null, Guid? excludeTransportId = null, CancellationToken ct = default);

    /// <summary>Every booked slot per vehicle — which car is taken when, and with
    /// which driver. Cancelled rides are excluded.</summary>
    Task<ApiResponse<List<VehicleBookingRow>>> GetBookingsAsync(
        Guid? eventId = null, DateTime? from = null, DateTime? to = null,
        Guid? vehicleId = null, Guid? driverId = null, CancellationToken ct = default);

    Task<ApiResponse<VehicleResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<VehicleResponse>> CreateAsync(CreateVehicleRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<VehicleResponse>> UpdateAsync(Guid id, UpdateVehicleRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
