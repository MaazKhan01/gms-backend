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
using Core.ViewModel.TransportApp;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Driver app. Every query is scoped to the caller's own DriverProfile — a driver
// can only ever see or touch transfers assigned to them.
public class TransportAppService(IUnitOfWork _unitOfWork, ILogger<TransportAppService> _logger) : ITransportAppService
{
    public async Task<ApiResponse<DriverStatsResponse>> GetStatsAsync(int userId, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<DriverStatsResponse>.NotFoundResponse("No driver profile for this user");

        // "Today" in UTC, matching how PickupTime is stored.
        var from = DateTime.UtcNow.Date;
        var to = from.AddDays(1);

        var today = await _unitOfWork.Transports.Query()
            .Where(t => t.DriverId == driverId && t.PickupTime >= from && t.PickupTime < to)
            .GroupBy(t => t.TripStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(params string[] statuses) => today.Where(x => statuses.Contains(x.Status)).Sum(x => x.Count);

        return ApiResponse<DriverStatsResponse>.SuccessResponse(new DriverStatsResponse
        {
            Today = today.Sum(x => x.Count),
            Completed = CountOf(TransportStatuses.Completed),
            InProgress = CountOf(TransportStatuses.InProgress),
            // Anything the driver still has to start.
            Pending = CountOf(TransportStatuses.Assigned, TransportStatuses.Arrived, TransportStatuses.Pending),
        });
    }

    public async Task<ApiResponse<List<DriverJobResponse>>> GetUpcomingJobsAsync(int userId, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<List<DriverJobResponse>>.NotFoundResponse("No driver profile for this user");

        // Everything still open, earliest pickup first. Jobs with no pickup time
        // sort last rather than disappearing.
        var jobs = await _unitOfWork.Transports.Query()
            .Where(t => t.DriverId == driverId && t.TripStatus != TransportStatuses.Completed)
            .OrderBy(t => t.PickupTime == null)
            .ThenBy(t => t.PickupTime)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<DriverJobResponse>>.SuccessResponse(jobs);
    }

    public async Task<ApiResponse<DriverJobResponse>> UpdateJobStatusAsync(
        int userId, Guid jobId, UpdateJobStatusRequest request, CancellationToken ct = default)
    {
        try
        {
            var driverId = await ResolveDriverIdAsync(userId, ct);
            if (driverId == null)
                return ApiResponse<DriverJobResponse>.NotFoundResponse("No driver profile for this user");

            var job = await _unitOfWork.Transports.Query()
                .FirstOrDefaultAsync(t => t.PublicId == jobId && t.DriverId == driverId, ct);
            if (job == null)
                return ApiResponse<DriverJobResponse>.NotFoundResponse("Job not found");

            // Only the single legal next step is accepted — no skipping ahead and
            // no going back, so a stale app screen can't rewrite history.
            var next = TransportStatuses.NextFor(job.TripStatus);
            var requested = request?.Status?.Trim();
            if (string.IsNullOrEmpty(requested))
                return ApiResponse<DriverJobResponse>.ErrorResponse("Status is required");
            if (next == null)
                return ApiResponse<DriverJobResponse>.ErrorResponse($"A job that is '{job.TripStatus}' cannot be advanced");
            if (!string.Equals(requested, next, StringComparison.OrdinalIgnoreCase))
                return ApiResponse<DriverJobResponse>.ErrorResponse($"Next status for this job is '{next}'");

            job.TripStatus = next;
            if (next == TransportStatuses.InProgress) job.ActualPickupTime = DateTime.UtcNow;
            if (next == TransportStatuses.Completed) job.ActualDropOffTime = DateTime.UtcNow;

            job.SetUpdateAudit(userId);
            _unitOfWork.Transports.Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            var updated = await _unitOfWork.Transports.Query()
                .Where(t => t.Id == job.Id)
                .Select(Project)
                .FirstOrDefaultAsync(ct);

            return ApiResponse<DriverJobResponse>.SuccessResponse(updated, $"Job {next}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver job {JobId}", jobId);
            return ApiResponse<DriverJobResponse>.ServerErrorResponse("An error occurred while updating the job");
        }
    }

    // The caller's DriverProfile.Id, or null when they aren't a driver.
    private async Task<int?> ResolveDriverIdAsync(int userId, CancellationToken ct)
        => (await _unitOfWork.DriverProfiles.Query()
            .Where(d => d.UserId == userId)
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync(ct));

    // Shared projection — stays an expression tree so EF translates it.
    private static readonly Expression<Func<Transport, DriverJobResponse>> Project = t => new DriverJobResponse
    {
        Id = t.PublicId,
        Status = t.TripStatus,
        GuestName = (t.Guest.FirstName + " " + t.Guest.LastName).Trim(),
        GuestTier = t.Guest.Tier,
        Pickup = t.PickupLocation == null ? null : t.PickupLocation.Address,
        Dropoff = t.DropoffLocation == null ? null : t.DropoffLocation.Address,
        PickupTime = t.PickupTime,
        DropoffTime = t.DropoffTime,
        ActualPickupTime = t.ActualPickupTime,
        ActualDropOffTime = t.ActualDropOffTime,
        VehicleNumber = t.Vehicle == null ? null : t.Vehicle.VehicleNumber,
        VehicleModel = t.Vehicle == null ? null : t.Vehicle.VehicleModel,
    };
}
