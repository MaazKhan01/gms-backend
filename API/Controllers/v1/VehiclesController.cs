using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Vehicle;
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
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToResponse(await _vehicles.GetAllAsync(ct));

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
