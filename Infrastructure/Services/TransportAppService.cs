using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Helpers;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.TransportApp;
using Core.ViewModel.Transportation;
using DomainPersistence.Entities;
using DomainPersistence.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Driver app. Every query is scoped to the caller's own DriverProfile — a driver
// can only ever see or touch transfers assigned to them.
public class TransportAppService(
    IUnitOfWork _unitOfWork,
    IRealTimeAlertService _realTimeAlerts, ILogger<TransportAppService> _logger) : ITransportAppService
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
            // Out on a job right now: en route, at pickup, or guest aboard.
            InProgress = CountOf(TransportStatuses.Active),
            // Booked but not set off yet, whenever its pickup falls.
            Pending = CountOf(TransportStatuses.Waiting),
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
            .Where(t => TransportStatuses.Live.Contains(t.TripStatus))
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
        if (jobs.Any(j => TransportStatuses.Active.Contains(j.Status)))
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

        // Not picked up yet: booked-but-not-started plus the two on-the-way steps.
        // Everything except in-transit (guest aboard) and the terminal statuses.
        var jobs = await ScopedJobs(driverId, eventId)
            .Where(t => TransportStatuses.Waiting.Contains(t.TripStatus)
                     || t.TripStatus == TransportStatuses.InProgress
                     || t.TripStatus == TransportStatuses.Arrived)
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

        if (profile == null)
            return ApiResponse<DriverProfileResponse>.NotFoundResponse("User not found");

        (profile.CountryCode, profile.PhoneNumber) = PhoneParts.Split(profile.PhoneNumber);

        return ApiResponse<DriverProfileResponse>.SuccessResponse(profile);
    }

    public async Task<ApiResponse<DriverProfileResponse>> ToggleOnlineAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            var profile = await _unitOfWork.DriverProfiles.Query()
                .FirstOrDefaultAsync(d => d.UserId == userId, ct);
            if (profile == null)
                return ApiResponse<DriverProfileResponse>.NotFoundResponse("No driver profile for this user");

            // Fixed drivers work their assigned jobs either way, so going
            // online/offline isn't theirs to decide.
            if (profile.DriverType != DriverType.Open)
                return ApiResponse<DriverProfileResponse>.ErrorResponse("Only open drivers can go online or offline");

            profile.IsOnline = !profile.IsOnline;
            profile.SetUpdateAudit(userId);
            _unitOfWork.DriverProfiles.Update(profile);
            await _unitOfWork.SaveChangesAsync(ct);

            var result = await GetProfileAsync(userId, ct);
            if (result.Success)
                result.Message = profile.IsOnline ? "You are now online" : "You are now offline";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling online state for user {UserId}", userId);
            return ApiResponse<DriverProfileResponse>.ServerErrorResponse("An error occurred while updating the online status");
        }
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
            // Two fields in, one column out. Either half omitted keeps the half
            // already on file, so sending just a new number doesn't drop the code.
            if (!string.IsNullOrWhiteSpace(request.CountryCode) || !string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var (currentCode, currentNumber) = PhoneParts.Split(user.Phone);
                user.Phone = PhoneParts.Join(
                    string.IsNullOrWhiteSpace(request.CountryCode) ? currentCode : request.CountryCode,
                    string.IsNullOrWhiteSpace(request.PhoneNumber) ? currentNumber : request.PhoneNumber);
            }

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

        // Jobs the driver is currently on: en route, at pickup, or guest aboard.
        // Most recently touched first — that's what "recent activity" means here.
        var jobs = await ScopedJobs(driverId, eventId)
            .Where(t => TransportStatuses.Active.Contains(t.TripStatus))
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<DriverJobResponse>>.SuccessResponse(jobs);
    }

    // Anything later than this past its planned time counts as a delay. ponytail:
    // fixed grace, move it to config if ops start arguing about the number.
    private const int OnTimeGraceMinutes = 5;

    public async Task<ApiResponse<DriverJobDetailResponse>> GetJobDetailAsync(
        int userId, Guid jobId, CancellationToken ct = default)
    {
        var driverId = await ResolveDriverIdAsync(userId, ct);
        if (driverId == null)
            return ApiResponse<DriverJobDetailResponse>.NotFoundResponse("No driver profile for this user");

        // Scoped to this driver's own jobs — someone else's id is a 404, not a peek.
        var job = await ScopedJobs(driverId, null)
            .Where(t => t.PublicId == jobId)
            .Select(t => new DriverJobDetailResponse
            {
                Id = t.PublicId,
                JobNumber = "VIP-" + t.Id,
                Status = t.TripStatus,
                EventId = t.Guest.Event == null ? null : t.Guest.Event.PublicId,
                EventName = t.Guest.Event == null ? null : t.Guest.Event.Title,
                GuestName = (t.Guest.FirstName + " " + t.Guest.LastName).Trim(),
                GuestTier = t.Guest.Tier,
                GuestType = t.Guest.GuestType,
                GuestOrganization = t.Guest.Organization,
                GuestEmail = t.Guest.Email,
                GuestPhotoUrl = t.Guest.PhotoUrl,
                Pickup = t.PickupLocation == null ? null : new JobLocationResponse
                {
                    Id = t.PickupLocation.PublicId,
                    Address = t.PickupLocation.Address,
                    Type = t.PickupLocation.Type,
                    Latitude = t.PickupLocation.Latitude,
                    Longitude = t.PickupLocation.Longitude,
                },
                Dropoff = t.DropoffLocation == null ? null : new JobLocationResponse
                {
                    Id = t.DropoffLocation.PublicId,
                    Address = t.DropoffLocation.Address,
                    Type = t.DropoffLocation.Type,
                    Latitude = t.DropoffLocation.Latitude,
                    Longitude = t.DropoffLocation.Longitude,
                },
                PickupTime = t.PickupTime,
                DropoffTime = t.DropoffTime,
                ActualPickupTime = t.ActualPickupTime,
                ActualDropOffTime = t.ActualDropOffTime,
                VehicleNumber = t.Vehicle == null ? null : t.Vehicle.VehicleNumber,
                VehicleModel = t.Vehicle == null ? null : t.Vehicle.VehicleModel,
                VehicleType = t.Vehicle == null || t.Vehicle.VehicleType == null ? null : t.Vehicle.VehicleType.Name,
                VehicleImage = t.Vehicle == null ? null : t.Vehicle.VehicleImage,
                VehicleCapacity = t.Vehicle == null ? null : t.Vehicle.Capacity,
            })
            .FirstOrDefaultAsync(ct);

        if (job == null)
            return ApiResponse<DriverJobDetailResponse>.NotFoundResponse("Job not found");

        // Same rule the status endpoint enforces, so the app knows which button to show.
        job.NextStatus = TransportStatuses.NextFor(job.Status);

        return ApiResponse<DriverJobDetailResponse>.SuccessResponse(job);
    }

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

    // ── Open pool: guest-requested jobs nobody has claimed yet ────────────────

    public async Task<ApiResponse<List<DriverJobResponse>>> GetAvailableJobsAsync(
        int userId, Guid? eventId = null, CancellationToken ct = default)
    {
        var driver = await ResolveDriverAsync(userId, ct);
        if (driver == null)
            return ApiResponse<List<DriverJobResponse>>.NotFoundResponse("No driver profile for this user");
        if (driver.Type != DriverType.Open)
            return ApiResponse<List<DriverJobResponse>>.ForbiddenResponse("Only open drivers can see requested jobs");

        // Not ScopedJobs — this pool is deliberately everyone's until claimed.
        var query = _unitOfWork.Transports.QueryNoTracking()
            .Where(t => t.TripStatus == TransportStatuses.New && t.DriverId == null);

        if (eventId != null)
            query = query.Where(t => t.Guest.Event.PublicId == eventId.Value);

        var jobs = await query
            .OrderBy(t => t.PickupTime == null)
            .ThenBy(t => t.PickupTime)
            .Select(Project)
            .ToListAsync(ct);

        return ApiResponse<List<DriverJobResponse>>.SuccessResponse(jobs);
    }

    public async Task<ApiResponse<DriverJobResponse>> AcceptJobAsync(
        int userId, Guid jobId, CancellationToken ct = default)
    {
        try
        {
            var driver = await ResolveDriverAsync(userId, ct);
            if (driver == null)
                return ApiResponse<DriverJobResponse>.NotFoundResponse("No driver profile for this user");
            // Same gate as the pool listing — a Fixed driver can't claim from it.
            if (driver.Type != DriverType.Open)
                return ApiResponse<DriverJobResponse>.ForbiddenResponse("Only open drivers can accept requested jobs");

            var driverId = driver.Id;

            // The claim is a single conditional UPDATE — the DriverId == null and
            // status == "new" tests are part of the WHERE, so two drivers tapping
            // Accept at the same moment can't both win: SQL Server serialises the
            // row write and the loser updates 0 rows. Nothing here reads-then-writes.
            var claimed = await _unitOfWork.Transports.Query()
                .Where(t => t.PublicId == jobId
                         && t.TripStatus == TransportStatuses.New
                         && t.DriverId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.DriverId, driverId)
                    .SetProperty(t => t.TripStatus, TransportStatuses.Assigned)
                    .SetProperty(t => t.UpdatedBy, userId)
                    .SetProperty(t => t.UpdatedAt, DateTime.UtcNow), ct);

            if (claimed == 0)
            {
                // Either it never existed, or somebody else got there first.
                var exists = await _unitOfWork.Transports.QueryNoTracking()
                    .AnyAsync(t => t.PublicId == jobId, ct);

                return exists
                    ? ApiResponse<DriverJobResponse>.ConflictResponse("This job has already been taken")
                    : ApiResponse<DriverJobResponse>.NotFoundResponse("Job not found");
            }

            var job = await _unitOfWork.Transports.QueryNoTracking()
                .Where(t => t.PublicId == jobId)
                .Select(Project)
                .FirstOrDefaultAsync(ct);

            return ApiResponse<DriverJobResponse>.SuccessResponse(job, "Job accepted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting job {JobId} for user {UserId}", jobId, userId);
            return ApiResponse<DriverJobResponse>.ServerErrorResponse("An error occurred while accepting the job");
        }
    }

    // ── Lifecycle: one endpoint per step, each asserting the status it expects ──
    // assigned → in-progress → arrived → in-transit → completed

    public Task<ApiResponse<DriverJobResponse>> StartJobAsync(int userId, Guid jobId, CancellationToken ct = default)
        => AdvanceAsync(userId, jobId, TransportStatuses.Assigned, TransportStatuses.InProgress, ct);

    public Task<ApiResponse<DriverJobResponse>> ArrivedAsync(int userId, Guid jobId, CancellationToken ct = default)
        => AdvanceAsync(userId, jobId, TransportStatuses.InProgress, TransportStatuses.Arrived, ct);

    public Task<ApiResponse<DriverJobResponse>> StartTripAsync(int userId, Guid jobId, CancellationToken ct = default)
        => AdvanceAsync(userId, jobId, TransportStatuses.Arrived, TransportStatuses.InTransit, ct);

    public Task<ApiResponse<DriverJobResponse>> CompleteAsync(int userId, Guid jobId, CancellationToken ct = default)
        => AdvanceAsync(userId, jobId, TransportStatuses.InTransit, TransportStatuses.Completed, ct);

    // The one status move, guarded: the job must currently be `expected`, so a
    // stale app screen can't skip a step or replay one it already did.
    private async Task<ApiResponse<DriverJobResponse>> AdvanceAsync(
        int userId, Guid jobId, string expected, string next, CancellationToken ct)
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

            if (!string.Equals(job.TripStatus, expected, StringComparison.OrdinalIgnoreCase))
                return ApiResponse<DriverJobResponse>.ErrorResponse(
                    $"This job is '{job.TripStatus}' — it must be '{expected}' to become '{next}'");

            job.TripStatus = next;
            // Guest aboard / dropped off are the two moments worth stamping.
            if (next == TransportStatuses.InTransit) job.ActualPickupTime = DateTime.UtcNow;
            if (next == TransportStatuses.Completed) job.ActualDropOffTime = DateTime.UtcNow;

            job.SetUpdateAudit(userId);
            _unitOfWork.Transports.Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.TransportStatusHistories.AddAsync(
                new TransportStatusHistory { TransportId = job.Id, Status = next, ChangedByUserId = userId }, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var guestTopic = next switch
            {
                TransportStatuses.Arrived => RealtimeTopics.TransportationDriverArrived,
                TransportStatuses.InProgress => RealtimeTopics.TransportationRideStarted,
                TransportStatuses.Completed => RealtimeTopics.TransportationRideCompleted,
                _ => null,
            };
            if (guestTopic != null)
                await _realTimeAlerts.SendToGroupAsync(guestTopic, $"guest:{job.GuestId}", "Ride update", $"Your ride is now '{next}'.");

            var updated = await _unitOfWork.Transports.Query()
                .Where(t => t.Id == job.Id)
                .Select(Project)
                .FirstOrDefaultAsync(ct);

            return ApiResponse<DriverJobResponse>.SuccessResponse(updated, $"Job {next}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving driver job {JobId} to {Status}", jobId, next);
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
        // Carries the raw stored value out of SQL; SplitPhone below turns it into
        // CountryCode + PhoneNumber (PhoneParts can't be translated to SQL).
        PhoneNumber = u.Phone,
        IsActive = u.IsActive,
        Role = u.Role == null ? null : u.Role.Name,
        DriverProfileId = u.DriverProfile == null ? null : u.DriverProfile.PublicId,
        DriverType = u.DriverProfile == null || u.DriverProfile.DriverType == null
            ? null
            : u.DriverProfile.DriverType.ToString(),
        IsOnline = u.DriverProfile != null && u.DriverProfile.IsOnline,
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

    // The caller's driver profile in the two shapes the open-job pool needs:
    // which row, and whether they're a Fixed or Open driver.
    private sealed record DriverRef(int Id, DriverType? Type, bool IsOnline);

    private Task<DriverRef> ResolveDriverAsync(int userId, CancellationToken ct)
        => _unitOfWork.DriverProfiles.QueryNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => new DriverRef(d.Id, d.DriverType, d.IsOnline))
            .FirstOrDefaultAsync(ct);

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
