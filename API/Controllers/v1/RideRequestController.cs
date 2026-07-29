using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

// Guest on-demand ride requests (Day 2) — scoped to the signed-in guest's own
// requests via ICurrentGuest, same pattern as SupportChatController's "my/*"
// side. Driver accept/open-list live on TransportAppController alongside the
// rest of the driver app surface.
[Route("api/v1/ride-requests")]
[Authorize]
[ApiVersion("1.0")]
public class RideRequestController(IRideRequestService _rideRequests, ICurrentGuest _currentGuest) : Controllers.BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRideRequestRequest request, CancellationToken ct)
        => ToResponse(await _rideRequests.CreateAsync(_currentGuest.GuestId, request, ct));

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => ToResponse(await _rideRequests.GetMineAsync(_currentGuest.GuestId, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ToResponse(await _rideRequests.CancelAsync(id, _currentGuest.GuestId, ct));
}
