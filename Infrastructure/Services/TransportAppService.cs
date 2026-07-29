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
    public async Task<ApiResponse<DriverStatsResponse>> GetStatsAsync(
        int userId, Guid? eventId = null, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<DriverStatsResponse>.NotFoundResponse("No driver profile for this user");

        var jobs = ScopedJobs(driverId, eventId);

        // "Today" in UTC, matching how PickupTime is stored.
        var from = DateTime.UtcNow.Date;
        var to = from.AddDays(1);

        // Completed / Pending are lifetime counts, not today's — everything the
        // driver still has ahead of them, and everything they've ever finished.
        var byStatus = await jobs
            .GroupBy(t => t.TripStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var todayByStatus = await jobs
            .Where(t => t.PickupTime >= from && t.PickupTime < to)
            .GroupBy(t => t.TripStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(params string[] statuses) => byStatus.Where(x => statuses.Contains(x.Status)).Sum(x => x.Count);

        return ApiResponse<DriverStatsResponse>.SuccessResponse(new DriverStatsResponse
        {
            Today = todayByStatus.Sum(x => x.Count),
            Completed = CountOf(TransportStatuses.Completed),
            InProgress = CountOf(TransportStatuses.Assigned),
            // Anything the driver still has to start, whenever its pickup falls.
            Pending = CountOf(TransportStatuses.Assigned, TransportStatuses.Arrived, TransportStatuses.Pending),
        });
    }

    public async Task<ApiResponse<List<DriverJobResponse>>> GetUpcomingJobsAsync(
        int userId, Guid? eventId = null, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<List<DriverJobResponse>>.NotFoundResponse("No driver profile for this user");

        // Everything still open, earliest pickup first. Jobs with no pickup time
        // sort last rather than disappearing.
        var jobs = await ScopedJobs(driverId, eventId)
            .Where(t => t.TripStatus == TransportStatuses.Assigned)
            .OrderBy(t => t.PickupTime == null)
            .ThenBy(t => t.PickupTime)
            .Select(Project)
            .ToListAsync(ct);

        MarkStartable(jobs);

        return ApiResponse<List<DriverJobResponse>>.SuccessResponse(jobs);
    }

    // Exactly one job in the list may show a Start button: the nearest assigned
    // one. Nothing is startable while another job is already under way — the
    // driver can't be in two cars at once.
    private static void MarkStartable(List<DriverJobResponse> jobs)
    {
        if (jobs.Any(j => j.Status == TransportStatuses.InProgress || j.Status == TransportStatuses.Arrived))
            return;

        // The list is already ordered earliest pickup first (nulls last).
        var next = jobs.FirstOrDefault(j => j.Status == TransportStatuses.Assigned);
        if (next != null) next.ShowStartButton = true;
    }

    public async Task<ApiResponse<List<DriverJobResponse>>> GetPendingPickupsAsync(
        int userId, Guid? eventId = null, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<List<DriverJobResponse>>.NotFoundResponse("No driver profile for this user");

        // Same set the stats endpoint counts as "Pending" — assigned to this driver
        // but the guest isn't on board yet.
        var jobs = await ScopedJobs(driverId, eventId)
            .Where(t => t.TripStatus == TransportStatuses.Assigned
                     || t.TripStatus == TransportStatuses.Arrived
                     || t.TripStatus == TransportStatuses.Pending)
            .OrderBy(t => t.PickupTime == null)
            .ThenBy(t => t.PickupTime)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<DriverJobResponse>>.SuccessResponse(jobs);
    }

    public async Task<ApiResponse<DriverProfileResponse>> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var profile = await UserWithDriverProfile()
            .Where(u => u.Id == userId)
            .Select(ProjectProfile)
            .FirstOrDefaultAsync(ct);

        return profile == null
            ? ApiResponse<DriverProfileResponse>.NotFoundResponse("User not found")
            : ApiResponse<DriverProfileResponse>.SuccessResponse(profile);
    }

    public async Task<ApiResponse<DriverProfileResponse>> UpdateProfileAsync(
        int userId, UpdateDriverProfileRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request == null)
                return ApiResponse<DriverProfileResponse>.ErrorResponse("Nothing to update");

            var user = await _unitOfWork.Users.Query()
                .Include(u => u.DriverProfile)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsDeleted != true, ct);
            if (user == null)
                return ApiResponse<DriverProfileResponse>.NotFoundResponse("User not found");

            // A driver may only touch these three things — role, licence, event
            // assignment and the rest stay admin-only.
            if (!string.IsNullOrWhiteSpace(request.FirstName)) user.FirstName = request.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(request.LastName)) user.LastName = request.LastName.Trim();
            if (!string.IsNullOrWhiteSpace(request.Phone)) user.Phone = request.Phone.Trim();

            user.SetUpdateAudit(userId);
            _unitOfWork.Users.Update(user);

            // Photo lives on the driver profile, so it needs one to land on.
            if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
            {
                if (user.DriverProfile == null)
                    return ApiResponse<DriverProfileResponse>.ErrorResponse("No driver profile for this user");

                user.DriverProfile.PhotoUrl = request.PhotoUrl.Trim();
                user.DriverProfile.SetUpdateAudit(userId);
                _unitOfWork.DriverProfiles.Update(user.DriverProfile);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            return await GetProfileAsync(userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver profile for user {UserId}", userId);
            return ApiResponse<DriverProfileResponse>.ServerErrorResponse("An error occurred while updating the profile");
        }
    }

    public async Task<ApiResponse<List<DriverJobResponse>>> GetJobsAsync(
        int userId, Guid? eventId = null, DateOnly? date = null, string status = null, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<List<DriverJobResponse>>.NotFoundResponse("No driver profile for this user");

        var query = ScopedJobs(driverId, eventId);

        // Each filter only narrows when it's supplied — no value means no filter.
        if (date != null)
        {
            var from = date.Value.ToDateTime(TimeOnly.MinValue);
            var to = from.AddDays(1);
            query = query.Where(t => t.PickupTime >= from && t.PickupTime < to);
        }

        var wanted = status?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(wanted))
        {
            if (!TransportStatuses.All.Contains(wanted))
                return ApiResponse<List<DriverJobResponse>>.ErrorResponse(
                    $"Unknown status '{status}'. Expected one of: {string.Join(", ", TransportStatuses.All)}");

            query = query.Where(t => t.TripStatus == wanted);
        }

        var jobs = await query
            .OrderBy(t => t.PickupTime == null)
            .ThenBy(t => t.PickupTime)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<DriverJobResponse>>.SuccessResponse(jobs);
    }

    public async Task<ApiResponse<List<DriverJobResponse>>> GetRecentActivityAsync(
        int userId, Guid? eventId = null, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<List<DriverJobResponse>>.NotFoundResponse("No driver profile for this user");

        // Jobs the driver is currently on: arrived at pickup or guest on board.
        // Most recently touched first — that's what "recent activity" means here.
        var jobs = await ScopedJobs(driverId, eventId)
            .Where(t => t.TripStatus == TransportStatuses.InProgress
                     || t.TripStatus == TransportStatuses.Arrived)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<DriverJobResponse>>.SuccessResponse(jobs);
    }

    // Anything later than this past its planned time counts as a delay. ponytail:
    // fixed grace, move it to config if ops start arguing about the number.
    private const int OnTimeGraceMinutes = 5;

    public async Task<ApiResponse<DriverSummaryResponse>> GetSummaryAsync(
        int userId, Guid? eventId = null, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<DriverSummaryResponse>.NotFoundResponse("No driver profile for this user");

        // Completed only — planned vs actual is meaningless before a job ends.
        var jobs = await ScopedJobs(driverId, eventId)
            .Where(t => t.TripStatus == TransportStatuses.Completed)
            .OrderByDescending(t => t.ActualDropOffTime ?? t.DropoffTime)
            .Select(t => new DriverJobPerformanceResponse
            {
                Id = t.PublicId,
                JobNumber = "VIP-" + t.Id,
                EventId = t.Guest.Event == null ? null : t.Guest.Event.PublicId,
                EventName = t.Guest.Event == null ? null : t.Guest.Event.Title,
                GuestName = (t.Guest.FirstName + " " + t.Guest.LastName).Trim(),
                Pickup = t.PickupLocation == null ? null : t.PickupLocation.Address,
                Dropoff = t.DropoffLocation == null ? null : t.DropoffLocation.Address,
                PickupTime = t.PickupTime,
                ActualPickupTime = t.ActualPickupTime,
                DropoffTime = t.DropoffTime,
                ActualDropOffTime = t.ActualDropOffTime,
            })
            .ToListAsync(ct);

        foreach (var j in jobs)
        {
            j.PickupDeltaMinutes = DeltaMinutes(j.PickupTime, j.ActualPickupTime);
            j.DropoffDeltaMinutes = DeltaMinutes(j.DropoffTime, j.ActualDropOffTime);
            j.EstimatedDurationMinutes = DeltaMinutes(j.PickupTime, j.DropoffTime);
            j.ActualDurationMinutes = DeltaMinutes(j.ActualPickupTime, j.ActualDropOffTime);
            j.DurationDeltaMinutes = j.EstimatedDurationMinutes == null || j.ActualDurationMinutes == null
                ? null
                : j.ActualDurationMinutes - j.EstimatedDurationMinutes;

            // Late at either end is a delay. Nothing recorded to compare against
            // counts as on time — a finished job with no measured lateness.
            var late = j.PickupDeltaMinutes > OnTimeGraceMinutes || j.DropoffDeltaMinutes > OnTimeGraceMinutes;
            j.Status = late ? "delayed" : "on-time";
        }

        return ApiResponse<DriverSummaryResponse>.SuccessResponse(new DriverSummaryResponse
        {
            CompletedJobs = jobs.Count,
            OnTimeJobs = jobs.Count(j => j.Status == "on-time"),
            DelayJobs = jobs.Count(j => j.Status == "delayed"),
            Jobs = jobs,
        });
    }

    // Whole minutes from planned to actual. Negative = early, null = either side missing.
    private static int? DeltaMinutes(DateTime? planned, DateTime? actual)
        => planned == null || actual == null ? null : (int)Math.Round((actual.Value - planned.Value).TotalMinutes);

    public async Task<ApiResponse<List<DriverEventResponse>>> GetEventsAsync(int userId, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<List<DriverEventResponse>>.NotFoundResponse("No driver profile for this user");

        // Only events the driver actually has transfers on — grouped so the app
        // gets the job count per event without a second call.
        var events = await ScopedJobs(driverId, null)
            .Where(t => t.Guest.Event != null)
            .GroupBy(t => new
            {
                t.Guest.Event.PublicId,
                t.Guest.Event.Title,
                t.Guest.Event.StartDate,
                t.Guest.Event.EndDate
            })
            .Select(g => new DriverEventResponse
            {
                Id = g.Key.PublicId,
                Title = g.Key.Title,
                StartDate = g.Key.StartDate,
                EndDate = g.Key.EndDate,
                JobCount = g.Count()
            })
            .OrderBy(e => e.StartDate == null)
            .ThenBy(e => e.StartDate)
            .ToListAsync(ct);

        return ApiResponse<List<DriverEventResponse>>.SuccessResponse(events);
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

    private IQueryable<User> UserWithDriverProfile()
        => _unitOfWork.Users.Query().Where(u => u.IsDeleted != true);

    // User row + DriverProfile row flattened into one payload.
    private static readonly Expression<Func<User, DriverProfileResponse>> ProjectProfile = u => new DriverProfileResponse
    {
        Id = u.PublicId,
        UserName = u.UserName,
        Email = u.Email,
        FirstName = u.FirstName,
        LastName = u.LastName,
        FullName = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
        Phone = u.Phone,
        IsActive = u.IsActive,
        Role = u.Role == null ? null : u.Role.Name,
        DriverProfileId = u.DriverProfile == null ? null : u.DriverProfile.PublicId,
        DriverType = u.DriverProfile == null || u.DriverProfile.DriverType == null
            ? null
            : u.DriverProfile.DriverType.ToString(),
        LicenseNumber = u.DriverProfile == null ? null : u.DriverProfile.LicenseNumber,
        LicenseExpiry = u.DriverProfile == null ? null : u.DriverProfile.LicenseExpiry,
        NationalityId = u.DriverProfile == null || u.DriverProfile.Nationality == null
            ? null
            : u.DriverProfile.Nationality.PublicId,
        Nationality = u.DriverProfile == null || u.DriverProfile.Nationality == null
            ? null
            : u.DriverProfile.Nationality.Name,
        PhotoUrl = u.DriverProfile == null ? null : u.DriverProfile.PhotoUrl,
    };

    // This driver's transfers, optionally narrowed to one event (via the guest).
    private IQueryable<Transport> ScopedJobs(int? driverId, Guid? eventId)
    {
        var q = _unitOfWork.Transports.Query().Where(t => t.DriverId == driverId);
        return eventId == null ? q : q.Where(t => t.Guest.Event.PublicId == eventId.Value);
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
        JobNumber = "VIP-" + t.Id,
        Status = t.TripStatus,
        EventId = t.Guest.Event == null ? null : t.Guest.Event.PublicId,
        EventName = t.Guest.Event == null ? null : t.Guest.Event.Title,
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
