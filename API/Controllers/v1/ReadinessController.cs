using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Readiness;

namespace API.Controllers.v1;

/// <summary>
/// Phase 6 — the pre-departure gate. Every item is derived from the booking data
/// on each read; only waivers are stored.
/// </summary>
[Route("api/v1/readiness")]
[Authorize]
[ApiVersion("1.0")]
public class ReadinessController(IReadinessService _readiness, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>Per-delegate checklist for a mission.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Readiness)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid eventId, [FromQuery] bool onlyNotReady = false, CancellationToken ct = default)
        => ToResponse(await _readiness.GetAsync(eventId, onlyNotReady, ct));

    /// <summary>Counts for the screen header, including how many delegates are
    /// missing each item.</summary>
    [HttpGet("summary")]
    [HasPermission(PermissionCodes.Readiness)]
    public async Task<IActionResult> GetSummary([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _readiness.GetSummaryAsync(eventId, ct));

    /// <summary>Overrides one missing item with a reason.</summary>
    [HttpPost("waive")]
    [HasPermission(PermissionCodes.Readiness, AccessLevel.Write)]
    public async Task<IActionResult> Waive([FromBody] WaiveReadinessRequest request, CancellationToken ct)
        => ToResponse(await _readiness.WaiveAsync(request, _currentUser.UserId, ct));

    /// <summary>Withdraws a waiver, putting the item back on the checklist.</summary>
    [HttpDelete("waivers/{waiverId:guid}")]
    [HasPermission(PermissionCodes.Readiness, AccessLevel.Write)]
    public async Task<IActionResult> RemoveWaiver(Guid waiverId, CancellationToken ct)
        => ToResponse(await _readiness.RemoveWaiverAsync(waiverId, _currentUser.UserId, ct));
}
