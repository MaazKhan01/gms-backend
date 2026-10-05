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
    [HasPermission(PermissionCodes.Guests)]
    public async Task<IActionResult> GetGuestOverview([FromQuery] GuestOverviewPagedRequest request, CancellationToken ct)
        => ToResponse(await _guestOverviewService.GetGuestOverviewAsync(request, ct));

    /// <summary>The person's whole history. The path id is a <c>Guest.PublicId</c>
    /// (GuestResponse.personId / GuestOverviewRow.id) — this screen spans events,
    /// so it is keyed on the person, not on one participation.</summary>
    [HttpGet("{personId:guid}")]
    [HasPermission(PermissionCodes.Guests)]
    public async Task<IActionResult> GetGuestOverviewDetail(Guid personId, CancellationToken ct)
        => ToResponse(await _guestOverviewService.GetGuestOverviewDetailAsync(personId, ct));
}
