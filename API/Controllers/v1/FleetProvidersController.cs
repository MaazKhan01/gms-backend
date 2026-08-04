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

[Route("api/v1/fleet-providers")]
[Authorize]
[ApiVersion("1.0")]
public class FleetProvidersController(IFleetProviderService _providers, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Same split as vehicles: reads open to any authenticated user (the vehicle
    // form needs the dropdown), writes gated on Travel.Manage.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToResponse(await _providers.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await _providers.GetByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Create([FromBody] CreateFleetProviderRequest request, CancellationToken ct)
        => ToResponse(await _providers.CreateAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFleetProviderRequest request, CancellationToken ct)
        => ToResponse(await _providers.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.TravelManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _providers.DeleteAsync(id, _currentUser.UserId, ct));
}
