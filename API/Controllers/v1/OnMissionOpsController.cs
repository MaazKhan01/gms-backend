using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.OnMissionOps;

namespace API.Controllers.v1;

/// <summary>
/// Phase 7 — the coordinator's ops desk while the delegation is on the ground:
/// headcount by subgroup, and the gathering notices sent to them.
/// </summary>
[Route("api/v1/on-mission-ops")]
[Authorize]
[ApiVersion("1.0")]
public class OnMissionOpsController(IOnMissionOpsService _ops, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>Subgroups with headcounts — the target picker and the headcount
    /// panel read the same list.</summary>
    [HttpGet("subgroups")]
    [HasPermission(PermissionCodes.OnMissionOps)]
    public async Task<IActionResult> GetSubgroups([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _ops.GetSubgroupsAsync(eventId, ct));

    /// <summary>The outbox — what was sent to the group, newest first.</summary>
    [HttpGet("notifications")]
    [HasPermission(PermissionCodes.OnMissionOps)]
    public async Task<IActionResult> GetNotifications([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _ops.GetNotificationsAsync(eventId, ct));

    /// <summary>Broadcasts to the whole delegation, or to one subgroup.</summary>
    [HttpPost("notifications")]
    [HasPermission(PermissionCodes.OnMissionOps, AccessLevel.Write)]
    public async Task<IActionResult> SendNotification(
        [FromBody] SendGatheringNotificationRequest request, CancellationToken ct)
        => ToResponse(await _ops.SendNotificationAsync(request, _currentUser.UserId, ct));
}
