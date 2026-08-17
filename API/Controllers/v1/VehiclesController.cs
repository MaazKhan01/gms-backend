using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Vehicle;
using DomainPersistence.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/vehicles")]
[Authorize]
[ApiVersion("1.0")]
public class VehiclesController(IVehicleService _vehicles, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Reads are open to any authenticated user — transport screens need the
    // fleet dropdown. Writes require Travel.Manage (the transport module owner).
    // usageType + unassigned are what the driver-invite picker sends: an Open driver
    // may only be given a Fixed car that nobody else holds.
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? eventId, [FromQuery] VehicleUsageType? usageType,
        [FromQuery] bool? unassigned, CancellationToken ct)
        => ToResponse(await _vehicles.GetAllAsync(eventId, usageType, unassigned, ct));

    // Vehicles free over [from, to) — the booking forms' dropdown feed, so an
    // already-taken car can't be picked in the first place. The server still
    // rejects a clashing save; this only keeps the UI honest.
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] DateTime from, [FromQuery] DateTime? to,
        [FromQuery] Guid? eventId, [FromQuery] Guid? excludeTransportId, CancellationToken ct)
        => ToResponse(await _vehicles.GetAvailableAsync(from, to, eventId, excludeTransportId, ct));

    // Fleet › Bookings: which vehicle is booked when, and with which driver.
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings(
        [FromQuery] Guid? eventId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId, CancellationToken ct)
        => ToResponse(await _vehicles.GetBookingsAsync(eventId, from, to, vehicleId, driverId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await _vehicles.GetByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest request, CancellationToken ct)
        => ToResponse(await _vehicles.CreateAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken ct)
        => ToResponse(await _vehicles.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _vehicles.DeleteAsync(id, _currentUser.UserId, ct));
}
