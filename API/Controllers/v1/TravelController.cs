using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Travel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/travel")]
[Authorize]
[ApiVersion("1.0")]
public class TravelController(ITravelService _travel, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Separate GET per lookup — each reads its own table.
    [HttpGet("lookups/flight-types")]
    public async Task<IActionResult> GetFlightTypes(CancellationToken ct)
        => ToResponse(await _travel.GetFlightTypesAsync(ct));

    [HttpGet("lookups/flight-classes")]
    public async Task<IActionResult> GetFlightClasses(CancellationToken ct)
        => ToResponse(await _travel.GetFlightClassesAsync(ct));

    [HttpGet("lookups/room-types")]
    public async Task<IActionResult> GetRoomTypes(CancellationToken ct)
        => ToResponse(await _travel.GetRoomTypesAsync(ct));

    [HttpGet("lookups/hotels")]
    public async Task<IActionResult> GetHotels(CancellationToken ct)
        => ToResponse(await _travel.GetHotelsAsync(ct));

    [HttpGet("lookups/locations")]
    public async Task<IActionResult> GetLocations(CancellationToken ct)
        => ToResponse(await _travel.GetLocationsAsync(ct));

    [HttpGet("lookups/vehicle-types")]
    public async Task<IActionResult> GetVehicleTypes(CancellationToken ct)
        => ToResponse(await _travel.GetVehicleTypesAsync(ct));

    // ── Per-event booking lists (admin travel tabs) ──────────────────────────
    [HttpGet("event/{eventId:guid}/flights")]
    public async Task<IActionResult> GetEventFlights(Guid eventId, CancellationToken ct)
        => ToResponse(await _travel.GetEventFlightsAsync(eventId, ct));

    [HttpGet("event/{eventId:guid}/accommodation")]
    public async Task<IActionResult> GetEventAccommodation(Guid eventId, CancellationToken ct)
        => ToResponse(await _travel.GetEventAccommodationsAsync(eventId, ct));

    [HttpGet("event/{eventId:guid}/transport")]
    public async Task<IActionResult> GetEventTransport(Guid eventId, CancellationToken ct)
        => ToResponse(await _travel.GetEventTransportsAsync(eventId, ct));

    // Prefill on edit.
    [HttpGet("guest/{guestId:guid}")]
    public async Task<IActionResult> GetGuestTravel(Guid guestId, CancellationToken ct)
        => ToResponse(await _travel.GetGuestTravelAsync(guestId, ct));

    // Upsert the selected sections (any subset of flight/accommodation/transport).
    [HttpPost("guest/{guestId:guid}")]
    public async Task<IActionResult> SaveGuestTravel(Guid guestId, [FromBody] GuestTravelRequest request, CancellationToken ct)
        => ToResponse(await _travel.SaveGuestTravelAsync(guestId, request, _currentUser.UserId, ct));

    // ── Manage the wizard dropdown options ──────────────────────────────────
    [HttpPost("lookups/flight-types")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateFlightType([FromBody] CreateNamedLookupRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateFlightTypeAsync(request, _currentUser.UserId, ct));

    [HttpPost("lookups/flight-classes")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateFlightClass([FromBody] CreateNamedLookupRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateFlightClassAsync(request, _currentUser.UserId, ct));

    [HttpPost("lookups/room-types")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateRoomType([FromBody] CreateNamedLookupRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateRoomTypeAsync(request, _currentUser.UserId, ct));

    [HttpPost("lookups/hotels")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateHotel([FromBody] CreateHotelRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateHotelAsync(request, _currentUser.UserId, ct));

    [HttpPost("lookups/vehicle-types")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateVehicleType([FromBody] CreateNamedLookupRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateVehicleTypeAsync(request, _currentUser.UserId, ct));
}
