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
/// Phase 9, steps 29–31 — the mission's single consolidated report.
///
/// Assemble → review → approve → publish → close. The two sign-offs are kept
/// apart because they are different people answering different questions: the
/// Head of Delegation says the text is right, Protocol says it may go out.
///
/// Every step is its own endpoint rather than one status setter, so each can
/// refuse for its own reason and say which rule was hit.
/// </summary>
[Route("api/v1/combined-report")]
[Authorize]
[ApiVersion("1.0")]
public class CombinedReportController(IMissionReportService _reports, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    [HttpGet]
    [HasPermission(PermissionCodes.CombinedReport)]
    public async Task<IActionResult> Get([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _reports.GetCombinedAsync(eventId, ct));

    /// <summary>Builds the body from the submitted delegate reports. Re-running
    /// it replaces the draft; it is refused once the report has been approved.</summary>
    [HttpPost("assemble")]
    [HasPermission(PermissionCodes.CombinedReport, AccessLevel.Write)]
    public async Task<IActionResult> Assemble([FromBody] CombinedReportActionRequest request, CancellationToken ct)
        => ToResponse(await _reports.AssembleAsync(request, _currentUser.UserId, ct));

    /// <summary>The Head of Delegation's edits to the assembled text.</summary>
    [HttpPut]
    [HasPermission(PermissionCodes.CombinedReport, AccessLevel.Write)]
    public async Task<IActionResult> Save([FromBody] SaveCombinedReportRequest request, CancellationToken ct)
        => ToResponse(await _reports.SaveCombinedAsync(request, _currentUser.UserId, ct));

    /// <summary>Draft → in review. Records the reviewer.</summary>
    [HttpPost("submit")]
    [HasPermission(PermissionCodes.CombinedReport, AccessLevel.Write)]
    public async Task<IActionResult> Submit([FromBody] CombinedReportActionRequest request, CancellationToken ct)
        => ToResponse(await _reports.SubmitForApprovalAsync(request, _currentUser.UserId, ct));

    /// <summary>In review → approved. Protocol's sign-off.</summary>
    [HttpPost("approve")]
    [HasPermission(PermissionCodes.CombinedReport, AccessLevel.Write)]
    public async Task<IActionResult> Approve([FromBody] CombinedReportActionRequest request, CancellationToken ct)
        => ToResponse(await _reports.ApproveCombinedAsync(request, _currentUser.UserId, ct));

    /// <summary>Approved → published, with the archived document's URL.</summary>
    [HttpPost("publish")]
    [HasPermission(PermissionCodes.CombinedReport, AccessLevel.Write)]
    public async Task<IActionResult> Publish([FromBody] CombinedReportActionRequest request, CancellationToken ct)
        => ToResponse(await _reports.PublishAsync(request, _currentUser.UserId, ct));

    /// <summary>Closes and archives the mission. Refused until the report is
    /// published — that is the whole of the gate, since finance settlement is
    /// out of scope for DMS.</summary>
    [HttpPost("close-mission")]
    [HasPermission(PermissionCodes.CombinedReport, AccessLevel.Write)]
    public async Task<IActionResult> Close([FromBody] CombinedReportActionRequest request, CancellationToken ct)
        => ToResponse(await _reports.CloseMissionAsync(request, _currentUser.UserId, ct));
}
