using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Lookup;
using Core.ViewModel.Venue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    public class VenueController(IVenueService _venueService, ILookupService _lookupService, ICurrentUser _currentUser) : Controllers.BaseApiController
    {
        // ── Venue reference data (dedicated routes, backed by the existing,
        // already-seeded VENUE_TYPE/ELEMENT_TYPE lookup categories) ───────────
        [HttpGet("types")]
        public async Task<IActionResult> GetVenueTypes(CancellationToken ct)
        {
            var result = await _lookupService.GetItemsByCategoryCodeAsync("VENUE_TYPE", false, ct);
            return ToResponse(result);
        }

        [HttpPost("types")]
        [HasPermission(PermissionCodes.VenueManage)]
        public async Task<IActionResult> CreateVenueType([FromBody] CreateVenueTypeDto request, CancellationToken ct)
        {
            var result = await _lookupService.CreateItemAsync(new LookupItemRequest
            {
                CategoryCode = "VENUE_TYPE",
                Code = Slugify(request.Name),
                Name = request.Name,
                NameAr = request.NameAr,
            }, _currentUser.UserId, ct);
            return ToResponse(result);
        }

        [HttpGet("element-types")]
        public async Task<IActionResult> GetElementTypes(CancellationToken ct)
        {
            var result = await _lookupService.GetItemsByCategoryCodeAsync("ELEMENT_TYPE", false, ct);
            return ToResponse(result);
        }

        [HttpPost("element-types")]
        [HasPermission(PermissionCodes.VenueManage)]
        public async Task<IActionResult> CreateElementType([FromBody] CreateElementTypeDto request, CancellationToken ct)
        {
            var result = await _lookupService.CreateItemAsync(new LookupItemRequest
            {
                CategoryCode = "ELEMENT_TYPE",
                Code = request.Code,
                Name = request.Name,
                NameAr = request.NameAr,
            }, _currentUser.UserId, ct);
            return ToResponse(result);
        }

        private static string Slugify(string s) => (s ?? "").Trim().ToUpperInvariant().Replace(" ", "_");

        [HttpPost]
        [HasPermission(PermissionCodes.VenueManage)]
        public async Task<IActionResult> CreateVenue([FromBody] CreateVenueRequest request, CancellationToken ct = default) {
            var result = await _venueService.CreateVenueAsync(request, _currentUser.UserId, ct);
            if (result.Success) {
                return ToResponse(result);
            }
            return ToResponse(result);
        }
        [HttpPost("{id:guid}")]
        [HasPermission(PermissionCodes.VenueManage)]
        public async Task<IActionResult> AddVenueBlock(Guid id, [FromQuery] Guid? sessionId, [FromQuery] Guid venueId, [FromBody] CreateVenueBlockDto request, CancellationToken ct)
        {
            var result = await _venueService.AddVenueBlockAsync(id, sessionId, venueId, request, ct);
            return ToResponse(result);
        }

        [HttpPost("box")]
        [HasPermission(PermissionCodes.VenueManage)]
        public async Task<IActionResult> CreateVenueBox([FromBody] CreateVenueBoxRequest request, CancellationToken ct = default)
        {
            var result = await _venueService.CreateVenueBoxAsync(request, request.EventId, _currentUser.UserId, ct);
            return ToResponse(result);
        }
        [HttpGet]
        //[HasPermission(PermissionCodes.VenueView)]
        public async Task<IActionResult> GetVenues(CancellationToken ct)
        {
            var result = await _venueService.GetVenuesAsync(ct);
            return ToResponse(result);
        }
        [HttpDelete("box/{id:guid}")]
        [HasPermission(PermissionCodes.VenueManage)]
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
        [HttpGet("{id:guid}")]
        //[HasPermission(PermissionCodes.VenueView)]
        public async Task<IActionResult> GetVenueById(Guid id, CancellationToken ct)
        {
            var result = await _venueService.GetVenueByIdAsync(id, ct);
            return ToResponse(result);
        }
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionCodes.VenueManage)]
        public async Task<IActionResult> DeleteVenue(Guid id, CancellationToken ct)
        {
            var result = await _venueService.DeleteVenueAsync(id, ct);
            return ToResponse(result);
        }
    }
}
