using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.FleetProvider;

namespace Core.Interfaces.Services;

// Providers belong to one event, so every call is scoped by the event's public
// Guid — same shape as the per-event service catalog (IServiceCatalogService).
public interface IFleetProviderService
{
    Task<ApiResponse<List<FleetProviderResponse>>> GetAllAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<FleetProviderResponse>> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default);
    Task<ApiResponse<FleetProviderResponse>> CreateAsync(Guid eventId, CreateFleetProviderRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<FleetProviderResponse>> UpdateAsync(Guid eventId, Guid id, UpdateFleetProviderRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid eventId, Guid id, int userId, CancellationToken ct = default);
}
