using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Vehicle;

namespace Core.Interfaces.Services;

public interface IVehicleService
{
    Task<ApiResponse<List<VehicleResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<VehicleResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<VehicleResponse>> CreateAsync(CreateVehicleRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<VehicleResponse>> UpdateAsync(Guid id, UpdateVehicleRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
