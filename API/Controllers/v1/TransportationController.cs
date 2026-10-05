using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

// Admin side of the transportation module — pre-scheduled rides and the
// eligible-driver pool per guest. Guest on-demand requests are POST
// api/v1/vip-app/transport-requests (they land as a Transport with status
// "new"); drivers see and claim them on TransportAppController.
[Route("api/v1/transportation")]
[Authorize]
[ApiVersion("1.0")]
public class TransportationController(ITransportationScheduleService _schedule, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    [HttpPost("schedule")]
    [HasPermission(PermissionCodes.Transportation, AccessLevel.Write)]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleRequest request, CancellationToken ct)
        => ToResponse(await _schedule.CreateScheduleAsync(request, _currentUser.UserId, ct));

    [HttpDelete("schedule/{id:guid}")]
    [HasPermission(PermissionCodes.Transportation, AccessLevel.Write)]
    public async Task<IActionResult> CancelSchedule(Guid id, CancellationToken ct)
        => ToResponse(await _schedule.CancelScheduleAsync(id, _currentUser.UserId, ct));

    /// <summary>One participation's rides. The path id is an
    /// <c>EventGuest.PublicId</c> (GuestResponse.id), not the person's id.</summary>
    [HttpGet("guest/{eventGuestId:guid}")]
    [HasPermission(PermissionCodes.Transportation)]
    public async Task<IActionResult> GetGuestSchedule(Guid eventGuestId, CancellationToken ct)
        => ToResponse(await _schedule.GetGuestScheduleAsync(eventGuestId, ct));

    [HttpGet("event/{eventId:guid}")]
    [HasPermission(PermissionCodes.Transportation)]
    public async Task<IActionResult> GetEventSchedule(Guid eventId, [FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _schedule.GetEventScheduleAsync(eventId, request, ct));

    /// <summary>The eligible-driver pool for one participation — path id is an
    /// <c>EventGuest.PublicId</c>.</summary>
    [HttpGet("guest/{eventGuestId:guid}/drivers")]
    [HasPermission(PermissionCodes.Transportation)]
    public async Task<IActionResult> GetAssignedDrivers(Guid eventGuestId, CancellationToken ct)
        => ToResponse(await _schedule.GetAssignedDriversAsync(eventGuestId, ct));
}
