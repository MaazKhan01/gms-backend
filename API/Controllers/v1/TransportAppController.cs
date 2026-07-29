using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.TransportApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

// ============================================================================
// Driver app — everything under api/v1/transport-app is scoped to the signed-in
// driver's own DriverProfile (resolved from ICurrentUser, drivers are system
// users). No permission attribute: drivers hold no admin permissions, and the
// DriverProfile lookup is the gate — a caller without one gets 404, and a
// caller with one only ever sees their own jobs.
// ============================================================================
[Route("api/v1/transport-app")]
[Authorize]
[ApiVersion("1.0")]
public class TransportAppController(ITransportAppService _transportApp, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // today = pickups falling today; completed/pending are lifetime counts.
    // eventId optional — omit it to count across all events.
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _transportApp.GetStatsAsync(_currentUser.UserId, eventId, ct));

    // Completed jobs only: completedJobs / onTimeJobs / delayJobs plus the
    // per-job planned-vs-actual cards. eventId optional.
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _transportApp.GetSummaryAsync(_currentUser.UserId, eventId, ct));

    // Events this driver has transfers on — the eventId values the two job
    // endpoints below accept.
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(CancellationToken ct)
        => ToResponse(await _transportApp.GetEventsAsync(_currentUser.UserId, ct));

    // Every open job assigned to this driver, earliest pickup first.
    // eventId optional — omit it for jobs across all events.
    [HttpGet("upcoming-jobs")]
    public async Task<IActionResult> GetUpcomingJobs([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _transportApp.GetUpcomingJobsAsync(_currentUser.UserId, eventId, ct));

    // Jobs not picked up yet (pending / assigned / arrived). eventId optional.
    [HttpGet("pending-pickups")]
    public async Task<IActionResult> GetPendingPickups([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _transportApp.GetPendingPickupsAsync(_currentUser.UserId, eventId, ct));

    // The driver's own profile: Users row + DriverProfiles row.
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
        => ToResponse(await _transportApp.GetProfileAsync(_currentUser.UserId, ct));

    // Driver edits their own name / phone / photo. Null or blank field = unchanged.
    // photoUrl comes from POST /api/v1/upload.
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateDriverProfileRequest request, CancellationToken ct)
        => ToResponse(await _transportApp.UpdateProfileAsync(_currentUser.UserId, request, ct));

    // All of this driver's jobs. eventId / date / status each filter only when sent.
    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs(
        [FromQuery] Guid? eventId, [FromQuery] DateOnly? date, [FromQuery] string status, CancellationToken ct)
        => ToResponse(await _transportApp.GetJobsAsync(_currentUser.UserId, eventId, date, status, ct));

    // What the driver is on right now: arrived / in-progress. eventId optional.
    [HttpGet("recent-activity")]
    public async Task<IActionResult> GetRecentActivity([FromQuery] Guid? eventId, CancellationToken ct)
        => ToResponse(await _transportApp.GetRecentActivityAsync(_currentUser.UserId, eventId, ct));

    // Advance a job one step: assigned → arrived → in-progress → completed.
    [HttpPut("jobs/{id:guid}/status")]
    public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] UpdateJobStatusRequest request, CancellationToken ct)
        => ToResponse(await _transportApp.UpdateJobStatusAsync(_currentUser.UserId, id, request, ct));
}
