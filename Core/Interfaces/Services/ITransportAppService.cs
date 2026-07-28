using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.TransportApp;

namespace Core.Interfaces.Services;

/// <summary>Driver-app reads/writes. `userId` is the signed-in user; the service
/// resolves their DriverProfile and scopes everything to it.</summary>
public interface ITransportAppService
{
    Task<ApiResponse<DriverStatsResponse>> GetStatsAsync(int userId, CancellationToken ct = default);
    Task<ApiResponse<List<DriverJobResponse>>> GetUpcomingJobsAsync(int userId, CancellationToken ct = default);
    Task<ApiResponse<DriverJobResponse>> UpdateJobStatusAsync(int userId, Guid jobId, UpdateJobStatusRequest request, CancellationToken ct = default);
}
