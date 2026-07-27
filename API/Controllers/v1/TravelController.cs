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
    // A section with an Id updates that specific booking in place; without one,
    // it adds a new booking alongside whatever the guest already has.
    [HttpPost("guest/{guestId:guid}")]
    public async Task<IActionResult> SaveGuestTravel(Guid guestId, [FromBody] GuestTravelRequest request, CancellationToken ct)
        => ToResponse(await _travel.SaveGuestTravelAsync(guestId, request, _currentUser.UserId, ct));

    // Remove one specific booking (a guest may have several of a kind).
    [HttpDelete("flight/{id:guid}")]
    public async Task<IActionResult> DeleteFlight(Guid id, CancellationToken ct)
        => ToResponse(await _travel.DeleteFlightAsync(id, ct));

    [HttpDelete("accommodation/{id:guid}")]
    public async Task<IActionResult> DeleteAccommodation(Guid id, CancellationToken ct)
        => ToResponse(await _travel.DeleteAccommodationAsync(id, ct));

    [HttpDelete("transport/{id:guid}")]
    public async Task<IActionResult> DeleteTransport(Guid id, CancellationToken ct)
        => ToResponse(await _travel.DeleteTransportAsync(id, ct));
}
