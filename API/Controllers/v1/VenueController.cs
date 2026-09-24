using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Venue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    public class VenueController(IVenueService _venueService, ICurrentUser _currentUser) : Controllers.BaseApiController
    {
        [HttpPost]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> CreateVenue([FromBody] CreateVenueRequest request, CancellationToken ct = default) {
            var result = await _venueService.CreateVenueAsync(request, _currentUser.UserId, ct);
            if (result.Success) {
                return ToResponse(result);
            }
            return ToResponse(result);
        }
        [HttpPost("{id:guid}/clone")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> CloneVenue(Guid id, [FromBody] CloneVenueRequest request, CancellationToken ct)
        {
            var result = await _venueService.CloneVenueAsync(id, request, _currentUser.UserId, ct);
            return ToResponse(result);
        }
        [HttpPost("{id:guid}")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> AddVenueBlock(Guid id, [FromQuery] Guid? sessionId, [FromQuery] Guid venueId, [FromBody] CreateVenueBlockDto request, CancellationToken ct)
        {
            var result = await _venueService.AddVenueBlockAsync(id, sessionId, venueId, request, ct);
            return ToResponse(result);
        }

        [HttpPost("box")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> CreateVenueBox([FromBody] CreateVenueBoxRequest request, CancellationToken ct = default)
        {
            var result = await _venueService.CreateVenueBoxAsync(request, request.EventId, _currentUser.UserId, ct);
            return ToResponse(result);
        }
        // Any-of: the venue list is a dropdown on the mission and session forms
        // and is read by Seating and Meetings, so gating it on Venue Config alone
        // emptied those pickers for roles that never touch the layout designer.
        [HttpGet]
        [HasPermission(AccessLevel.Read,
            PermissionCodes.VenueConfig, PermissionCodes.Venues, PermissionCodes.Events,
            PermissionCodes.Seating, PermissionCodes.Meetings)]
        public async Task<IActionResult> GetVenues(CancellationToken ct)
        {
            var result = await _venueService.GetVenuesAsync(ct);
            return ToResponse(result);
        }
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> UpdateVenue(Guid id, [FromBody] UpdateVenueRequest request, CancellationToken ct)
        {
            var result = await _venueService.UpdateVenueAsync(id, request, _currentUser.UserId, ct);
            return ToResponse(result);
        }
        [HttpDelete("box/{id:guid}")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> DeleteVenueBox(
    Guid id,
    [FromQuery] Guid venueId,
    [FromQuery] Guid eventId,
    [FromQuery] Guid sessionId,
    CancellationToken ct = default)
        {
            if (id == Guid.Empty)
                return BadRequest(ApiResponse<object>.ErrorResponse("Venue box id is required."));

            if (venueId == Guid.Empty)
                return BadRequest(ApiResponse<object>.ErrorResponse("Venue id is required."));

            var hasEvent = eventId != Guid.Empty;
            var hasSession = sessionId != Guid.Empty;

            if (hasEvent == hasSession)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("Either eventId or sessionId must be provided, but not both."));
            }

            var result = await _venueService.DeleteVenueBoxAsync(
                id,
                venueId,
                eventId,
                sessionId,
                ct);

            return ToResponse(result);
        }
        // Same any-of set as the list above.
        [HttpGet("{id:guid}")]
        [HasPermission(AccessLevel.Read,
            PermissionCodes.VenueConfig, PermissionCodes.Venues, PermissionCodes.Events,
            PermissionCodes.Seating, PermissionCodes.Meetings)]
        public async Task<IActionResult> GetVenueById(Guid id, CancellationToken ct)
        {
            var result = await _venueService.GetVenueByIdAsync(id, ct);
            return ToResponse(result);
        }
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> DeleteVenue(Guid id, CancellationToken ct)
        {
            var result = await _venueService.DeleteVenueAsync(id, ct);
            return ToResponse(result);
        }

        // ── Venue reference data ─────────────────────────────────────────────
        [HttpGet("types")]
        public async Task<IActionResult> GetVenueTypes(CancellationToken ct)
            => ToResponse(await _venueService.GetVenueTypesAsync(ct));

        [HttpPost("types")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> CreateVenueType([FromBody] CreateVenueTypeRequest request, CancellationToken ct)
            => ToResponse(await _venueService.CreateVenueTypeAsync(request, _currentUser.UserId, ct));

        [HttpGet("element-types")]
        public async Task<IActionResult> GetElementTypes(CancellationToken ct)
            => ToResponse(await _venueService.GetElementTypesAsync(ct));

        [HttpPost("element-types")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> CreateElementType([FromBody] CreateElementTypeRequest request, CancellationToken ct)
            => ToResponse(await _venueService.CreateElementTypeAsync(request, _currentUser.UserId, ct));

        [HttpPut("element-types/{id:guid}")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> UpdateElementType(Guid id, [FromBody] UpdateElementTypeRequest request, CancellationToken ct)
            => ToResponse(await _venueService.UpdateElementTypeAsync(id, request, _currentUser.UserId, ct));

        [HttpDelete("element-types/{id:guid}")]
        [HasPermission(PermissionCodes.VenueConfig, AccessLevel.Write)]
        public async Task<IActionResult> DeleteElementType(Guid id, CancellationToken ct)
            => ToResponse(await _venueService.DeleteElementTypeAsync(id, _currentUser.UserId, ct));
    }
}
