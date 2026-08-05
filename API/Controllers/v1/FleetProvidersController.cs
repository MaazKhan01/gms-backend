using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.FleetProvider;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

// Nested under the event because providers are contracted per event — the route
// makes the scope impossible to forget, the way the per-event service catalog
// does it (ServicesController).
[Route("api/v1/events/{eventId:guid}/fleet-providers")]
[Authorize]
[ApiVersion("1.0")]
public class FleetProvidersController(IFleetProviderService _providers, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Same split as vehicles: reads open to any authenticated user (the vehicle
    // form needs the dropdown), writes gated on Travel.Manage.
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid eventId, CancellationToken ct)
        => ToResponse(await _providers.GetAllAsync(eventId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid eventId, Guid id, CancellationToken ct)
        => ToResponse(await _providers.GetByIdAsync(eventId, id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Create(Guid eventId, [FromBody] CreateFleetProviderRequest request, CancellationToken ct)
        => ToResponse(await _providers.CreateAsync(eventId, request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Update(Guid eventId, Guid id, [FromBody] UpdateFleetProviderRequest request, CancellationToken ct)
        => ToResponse(await _providers.UpdateAsync(eventId, id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
        => ToResponse(await _providers.DeleteAsync(eventId, id, _currentUser.UserId, ct));
}
