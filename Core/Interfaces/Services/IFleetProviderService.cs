using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.FleetProvider;

namespace Core.Interfaces.Services;

public interface IFleetProviderService
{
    Task<ApiResponse<List<FleetProviderResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<FleetProviderResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<FleetProviderResponse>> CreateAsync(CreateFleetProviderRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<FleetProviderResponse>> UpdateAsync(Guid id, UpdateFleetProviderRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
