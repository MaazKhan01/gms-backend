using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Seating;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{


    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    public class SeatingController(ISeatingService _seatingService, ICurrentUser _currentUser) : Controllers.BaseApiController
    {
        [HttpPost]
        [HasPermission(PermissionCodes.Seating, AccessLevel.Write)]
        public async Task<IActionResult> AssignSeatToGuest([FromBody] RequestSeatAssignDto request, CancellationToken ct=default)
        {
            var result = await _seatingService.AssignSeatToGuestAsync(request, _currentUser.UserId, ct);
            return ToResponse(result);
        }

        [HttpDelete("{seatId:guid}")]
        [HasPermission(PermissionCodes.Seating, AccessLevel.Write)]
        public async Task<IActionResult> UnassignSeat(Guid seatId, [FromQuery] Guid venueBoxId, [FromQuery] Guid eventId, [FromQuery] Guid? sessionId, CancellationToken ct = default)
        {
            var result = await _seatingService.UnassignSeatAsync(seatId, venueBoxId, eventId, sessionId, ct);
            return ToResponse(result);
        }

        [HttpGet("box/{venueBoxId:guid}")]
        [HasPermission(PermissionCodes.Seating)]
        public async Task<IActionResult> GetSeatAssignments(Guid venueBoxId, [FromQuery] Guid eventId, [FromQuery] Guid? sessionId, CancellationToken ct = default)
        {
            var result = await _seatingService.GetSeatAssignmentsAsync(venueBoxId, eventId, sessionId, ct);
            return ToResponse(result);
        }

        // Every seat this participation currently holds, across sessions/scopes —
        // used by the Guests screen to warn before removing a seated guest.
        // The path id is an EventGuest.PublicId: seating is per event.
        [HttpGet("guest/{eventGuestId:guid}")]
        [HasPermission(PermissionCodes.Seating)]
        public async Task<IActionResult> GetGuestSeatAssignments(Guid eventGuestId, CancellationToken ct = default)
        {
            var result = await _seatingService.GetGuestSeatAssignmentsAsync(eventGuestId, ct);
            return ToResponse(result);
        }
    }
}
