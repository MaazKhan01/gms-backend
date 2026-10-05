using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Reports;

namespace API.Controllers.v1;

/// <summary>
/// Phase 9, step 28 — each delegate's own report.
///
/// DMS fills in the trip facts it already holds (dates, hotel, flights,
/// transport, sessions attended) so the delegate only writes the part nobody
/// else can write: what happened and what came of it.
///
/// All of this is deliberately available AFTER the mission has completed. The
/// completion guard stops new delegates and bookings; reporting is the work that
/// only starts once the mission is over.
/// </summary>
[Route("api/v1/post-mission-reports")]
[Authorize]
[ApiVersion("1.0")]
public class PostMissionReportsController(IMissionReportService _reports, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>The roster with each delegate's report state and trip facts.
    /// <paramref name="status"/>: not_submitted | submitted | approved |
    /// outstanding (everything still to chase).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.PostMissionReports)]
    public async Task<IActionResult> GetAll([FromQuery] Guid eventId, [FromQuery] string status, CancellationToken ct)
        => ToResponse(await _reports.GetReportsAsync(eventId, status, ct));

    [HttpGet("summary")]
    [HasPermission(PermissionCodes.PostMissionReports)]
    public async Task<IActionResult> GetSummary([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _reports.GetReportSummaryAsync(eventId, ct));

    /// <summary>Saves the narrative, and submits it when `submit` is true.</summary>
    [HttpPut]
    [HasPermission(PermissionCodes.PostMissionReports, AccessLevel.Write)]
    public async Task<IActionResult> Save([FromBody] SaveReportRequest request, CancellationToken ct)
        => ToResponse(await _reports.SaveReportAsync(request, _currentUser.UserId, ct));

    /// <summary>Accepts a submitted report, after which it can no longer be edited.</summary>
    [HttpPost("{participationId:guid}/approve")]
    [HasPermission(PermissionCodes.PostMissionReports, AccessLevel.Write)]
    public async Task<IActionResult> Approve(Guid participationId, CancellationToken ct)
        => ToResponse(await _reports.ApproveReportAsync(participationId, _currentUser.UserId, ct));

    /// <summary>Chases outstanding reports. An empty `ids` chases every one of
    /// them, which is the normal case.</summary>
    [HttpPost("nudge")]
    [HasPermission(PermissionCodes.PostMissionReports, AccessLevel.Write)]
    public async Task<IActionResult> Nudge([FromBody] NudgeReportsRequest request, CancellationToken ct)
        => ToResponse(await _reports.NudgeAsync(request, _currentUser.UserId, ct));
}
