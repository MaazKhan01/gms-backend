using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.TransportApp;
using Core.ViewModel.Transportation;

namespace Core.Interfaces.Services;

/// <summary>Driver-app reads/writes. `userId` is the signed-in user; the service
/// resolves their DriverProfile and scopes everything to it.</summary>
public interface ITransportAppService
{
    /// <param name="eventId">Event public id. Null = every event.</param>
    Task<ApiResponse<DriverStatsResponse>> GetStatsAsync(int userId, Guid? eventId = null, CancellationToken ct = default);

    /// <param name="eventId">Event public id. Null = every event.</param>
    Task<ApiResponse<List<DriverJobResponse>>> GetUpcomingJobsAsync(int userId, Guid? eventId = null, CancellationToken ct = default);

    /// <summary>Jobs the driver hasn't picked up yet (pending / assigned / arrived).</summary>
    /// <param name="eventId">Event public id. Null = every event.</param>
    Task<ApiResponse<List<DriverJobResponse>>> GetPendingPickupsAsync(int userId, Guid? eventId = null, CancellationToken ct = default);
    /// <summary>The caller's own profile: User row + DriverProfile row.</summary>
    Task<ApiResponse<DriverProfileResponse>> GetProfileAsync(int userId, CancellationToken ct = default);

    /// <summary>Driver edits their own name / phone / photo. Null fields stay as they are.</summary>
    Task<ApiResponse<DriverProfileResponse>> UpdateProfileAsync(int userId, UpdateDriverProfileRequest request, CancellationToken ct = default);

    /// <summary>Every job of this driver's. Each filter applies only when supplied.</summary>
    /// <param name="eventId">Event public id, or null for all events.</param>
    /// <param name="date">Pickup date, or null for all dates.</param>
    /// <param name="status">TripStatus, or null for all statuses.</param>
    Task<ApiResponse<List<DriverJobResponse>>> GetJobsAsync(int userId, Guid? eventId = null, DateOnly? date = null, string status = null, CancellationToken ct = default);

    /// <summary>Jobs the driver is on right now (arrived / in-progress), newest touched first.</summary>
    /// <param name="eventId">Event public id. Null = every event.</param>
    Task<ApiResponse<List<DriverJobResponse>>> GetRecentActivityAsync(int userId, Guid? eventId = null, CancellationToken ct = default);

    /// <summary>Events this driver has transfers assigned on.</summary>
    Task<ApiResponse<List<DriverEventResponse>>> GetEventsAsync(int userId, CancellationToken ct = default);

    Task<ApiResponse<DriverJobResponse>> UpdateJobStatusAsync(int userId, Guid jobId, UpdateJobStatusRequest request, CancellationToken ct = default);

    /// <summary>Opt in/out of on-demand ride-request broadcasts.</summary>
    Task<ApiResponse<bool>> ToggleAvailabilityAsync(int userId, bool isAvailable, CancellationToken ct = default);

    /// <summary>Every open on-demand ride request an available driver could accept.</summary>
    Task<ApiResponse<List<RideRequestRow>>> GetOpenRideRequestsAsync(int userId, CancellationToken ct = default);

    /// <summary>First driver to accept wins — see RideRequestService.AcceptAsync.</summary>
    Task<ApiResponse<RideRequestRow>> AcceptRideRequestAsync(int userId, Guid rideRequestId, CancellationToken ct = default);
}
