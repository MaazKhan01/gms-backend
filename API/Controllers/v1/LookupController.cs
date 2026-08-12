using Core.Authorization;
using System.Collections.Generic;
using Core.ViewModel.Common;
using Core.Constants;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Travel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/lookups")]
[Authorize]
[ApiVersion("1.0")]
public class LookupController(ILookupService _lookupService, ITravelService _travel, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Code-defined guest option sets (tier, type, statuses) for form dropdowns.
    // Any authenticated user can read these — they're static reference lists.
    [HttpGet("enums/guest")]
    public IActionResult GetGuestEnums()
        => ToResponse(_lookupService.GetGuestEnums());

    // Driver engagement types (fixed / open) for the driver invite form.
    [HttpGet("enums/driver-types")]
    public IActionResult GetDriverTypes()
        => ToResponse(_lookupService.GetDriverTypes());

    // Transfer lifecycle statuses — the values the transport-app job filters take.
    [HttpGet("enums/transport-statuses")]
    public IActionResult GetTransportStatuses()
        => ToResponse(_lookupService.GetTransportStatuses());

    // Lookup tables a service form field can draw its options from. Lets the
    // form builder offer "Airports", "Hotels" … without hardcoding the list in
    // the client. See Core.Constants.ServiceLookupSources.
    [HttpGet("service-sources")]
    public IActionResult GetServiceLookupSources()
        => ToResponse(ApiResponse<IReadOnlyList<ServiceLookupSource>>.SuccessResponse(ServiceLookupSources.All));

    // Flight directions — a code-defined enum, not a table.
    [HttpGet("flight-types")]
    public IActionResult GetFlightTypes()
        => ToResponse(_lookupService.GetFlightTypes());

    // ── Travel lookups — separate GET per lookup, each reads its own table ────
    [HttpGet("flight-classes")]
    public async Task<IActionResult> GetFlightClasses(CancellationToken ct)
        => ToResponse(await _travel.GetFlightClassesAsync(ct));

    [HttpGet("room-types")]
    public async Task<IActionResult> GetRoomTypes(CancellationToken ct)
        => ToResponse(await _travel.GetRoomTypesAsync(ct));

    [HttpGet("hotels")]
    public async Task<IActionResult> GetHotels(CancellationToken ct)
        => ToResponse(await _travel.GetHotelsAsync(ct));

    [HttpGet("locations")]
    public async Task<IActionResult> GetLocations(CancellationToken ct)
        => ToResponse(await _travel.GetLocationsAsync(ct));

    [HttpGet("vehicle-types")]
    public async Task<IActionResult> GetVehicleTypes(CancellationToken ct)
        => ToResponse(await _travel.GetVehicleTypesAsync(ct));

    // Pass from (+ optional to) to get only drivers free over that window — the
    // booking form's dropdown feed, so an already-assigned driver can't be picked.
    [HttpGet("drivers")]
    public async Task<IActionResult> GetDrivers(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? excludeTransportId, CancellationToken ct)
        => ToResponse(await _travel.GetDriversAsync(from, to, excludeTransportId, ct));

    [HttpGet("airports")]
    public async Task<IActionResult> GetAirports(CancellationToken ct)
        => ToResponse(await _travel.GetAirportsAsync(ct));

    // ── Manage the wizard dropdown options ──────────────────────────────────
    [HttpPost("flight-classes")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateFlightClass([FromBody] CreateNamedLookupRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateFlightClassAsync(request, _currentUser.UserId, ct));

    [HttpPost("room-types")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateRoomType([FromBody] CreateNamedLookupRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateRoomTypeAsync(request, _currentUser.UserId, ct));

    [HttpPost("hotels")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateHotel([FromBody] CreateHotelRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateHotelAsync(request, _currentUser.UserId, ct));

    // Hotels are editable (unlike the name-only lookups): the VIP app reads their
    // address and image, and rows created before the address was required have to
    // be fixable without a DB trip.
    [HttpPut("hotels/{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> UpdateHotel(Guid id, [FromBody] CreateHotelRequest request, CancellationToken ct)
        => ToResponse(await _travel.UpdateHotelAsync(id, request, _currentUser.UserId, ct));

    [HttpPost("vehicle-types")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateVehicleType([FromBody] CreateNamedLookupRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateVehicleTypeAsync(request, _currentUser.UserId, ct));

    [HttpPost("airports")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateAirport([FromBody] CreateAirportRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateAirportAsync(request, _currentUser.UserId, ct));

    [HttpPost("locations")]
    //[HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> CreateLocation([FromBody] LocationRequest request, CancellationToken ct)
        => ToResponse(await _travel.CreateLocationAsync(request, _currentUser.UserId, ct));

    [HttpPut("locations/{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] LocationRequest request, CancellationToken ct)
        => ToResponse(await _travel.UpdateLocationAsync(id, request, _currentUser.UserId, ct));
}
