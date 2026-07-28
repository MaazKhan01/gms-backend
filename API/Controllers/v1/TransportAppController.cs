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
    // Today's counts: total, completed, pending (not started), in progress.
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
        => ToResponse(await _transportApp.GetStatsAsync(_currentUser.UserId, ct));

    // Every open job assigned to this driver, earliest pickup first.
    [HttpGet("upcoming-jobs")]
    public async Task<IActionResult> GetUpcomingJobs(CancellationToken ct)
        => ToResponse(await _transportApp.GetUpcomingJobsAsync(_currentUser.UserId, ct));

    // Advance a job one step: assigned → arrived → in-progress → completed.
    [HttpPut("jobs/{id:guid}/status")]
    public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] UpdateJobStatusRequest request, CancellationToken ct)
        => ToResponse(await _transportApp.UpdateJobStatusAsync(_currentUser.UserId, id, request, ct));
}
