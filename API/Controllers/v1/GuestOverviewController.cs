using Core.Authorization;
using Core.Common;
using Core.Interfaces.Services;
using Core.ViewModel.Guest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

// Cross-event "all guests in the system" listing + on-demand detail, backing
// the Guest Overview screen. Distinct from GuestController, whose GET is
// scoped to one eventId — see GuestOverviewService for why this is its own
// service rather than an extra mode bolted onto that one.
[Route("api/v1/guest-overview")]
[Authorize]
[ApiVersion("1.0")]
public class GuestOverviewController(IGuestOverviewService _guestOverviewService) : Controllers.BaseApiController
{
    [HttpGet]
    [HasPermission(PermissionCodes.GuestsView)]
    public async Task<IActionResult> GetGuestOverview([FromQuery] GuestOverviewPagedRequest request, CancellationToken ct)
        => ToResponse(await _guestOverviewService.GetGuestOverviewAsync(request, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.GuestsView)]
    public async Task<IActionResult> GetGuestOverviewDetail(Guid id, CancellationToken ct)
        => ToResponse(await _guestOverviewService.GetGuestOverviewDetailAsync(id, ct));
}
