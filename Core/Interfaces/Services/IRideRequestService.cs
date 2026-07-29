using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Transportation;

namespace Core.Interfaces.Services;

// Guest on-demand ride requests (Day 2). Accept is concurrency-safe — see
// RideRequestService.AcceptAsync (RideRequest.RowVersion optimistic token).
public interface IRideRequestService
{
    Task<ApiResponse<RideRequestRow>> CreateAsync(int guestId, CreateRideRequestRequest request, CancellationToken ct = default);

    Task<ApiResponse<List<RideRequestRow>>> GetMineAsync(int guestId, CancellationToken ct = default);

    Task<ApiResponse<bool>> CancelAsync(Guid rideRequestId, int? guestId, CancellationToken ct = default);

    // Every open request an available driver could take (not scoped to one driver).
    Task<ApiResponse<List<RideRequestRow>>> GetOpenAsync(CancellationToken ct = default);

    Task<ApiResponse<RideRequestRow>> AcceptAsync(int driverId, Guid rideRequestId, CancellationToken ct = default);
}
