using System;
using Core.ViewModel.Common;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.ServiceCatalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

/// <summary>
/// The global service catalogue. Not event-scoped — see docs/service-levels-v2.md.
/// </summary>
[ApiController]
[Route("api/v1/services")]
[Authorize]
public class ServicesController(IServiceCatalogService _catalog, ICurrentUser _currentUser) : BaseApiController
{
    // Reads stay open to any signed-in user: the service level builder, the guest
    // form and the guest's services tab all need the catalogue, and gating them
    // behind Services.View would 403 users who only manage guests.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
        => ToResponse(await _catalog.GetServicesAsync(includeInactive, ct));

    [HttpGet("{serviceId:guid}")]
    public async Task<IActionResult> GetById(Guid serviceId, CancellationToken ct)
        => ToResponse(await _catalog.GetServiceByIdAsync(serviceId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.ServicesManage)]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request, CancellationToken ct)
        => ToResponse(await _catalog.CreateServiceAsync(request, _currentUser.UserId, ct));

    [HttpPut("{serviceId:guid}")]
    [HasPermission(PermissionCodes.ServicesManage)]
    public async Task<IActionResult> Update(Guid serviceId, [FromBody] UpdateServiceRequest request, CancellationToken ct)
        => ToResponse(await _catalog.UpdateServiceAsync(serviceId, request, _currentUser.UserId, ct));

    [HttpDelete("{serviceId:guid}")]
    [HasPermission(PermissionCodes.ServicesManage)]
    public async Task<IActionResult> Delete(Guid serviceId, CancellationToken ct)
        => ToResponse(await _catalog.DeleteServiceAsync(serviceId, _currentUser.UserId, ct));

    // Every guest in an event who has this service — the operational listing
    // that replaces the fixed Flights / Hotel / Transfers tabs.
    [HttpGet("{serviceId:guid}/entries")]
    public async Task<IActionResult> GetEntries(
        Guid serviceId, [FromQuery] Guid eventId, [FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _catalog.GetServiceEntriesAsync(serviceId, eventId, request, ct));
}

/// <summary>Global service levels, and the services assigned to each.</summary>
[ApiController]
[Route("api/v1/service-levels")]
[Authorize]
public class ServiceLevelsController(IServiceCatalogService _catalog, ICurrentUser _currentUser) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
        => ToResponse(await _catalog.GetServiceLevelsAsync(includeInactive, ct));

    [HttpGet("{levelId:guid}")]
    public async Task<IActionResult> GetById(Guid levelId, CancellationToken ct)
        => ToResponse(await _catalog.GetServiceLevelByIdAsync(levelId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.ServiceLevelsManage)]
    public async Task<IActionResult> Create([FromBody] CreateServiceLevelRequest request, CancellationToken ct)
        => ToResponse(await _catalog.CreateServiceLevelAsync(request, _currentUser.UserId, ct));

    [HttpPut("{levelId:guid}")]
    [HasPermission(PermissionCodes.ServiceLevelsManage)]
    public async Task<IActionResult> Update(Guid levelId, [FromBody] UpdateServiceLevelRequest request, CancellationToken ct)
        => ToResponse(await _catalog.UpdateServiceLevelAsync(levelId, request, _currentUser.UserId, ct));

    [HttpDelete("{levelId:guid}")]
    [HasPermission(PermissionCodes.ServiceLevelsManage)]
    public async Task<IActionResult> Delete(Guid levelId, CancellationToken ct)
        => ToResponse(await _catalog.DeleteServiceLevelAsync(levelId, _currentUser.UserId, ct));
}

/// <summary>A guest's service checklist and the entries completed against it.</summary>
[ApiController]
[Route("api/v1/guests/{guestId:guid}/services")]
[Authorize]
public class GuestServicesController(IServiceCatalogService _catalog, ICurrentUser _currentUser) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetPlan(Guid guestId, CancellationToken ct)
        => ToResponse(await _catalog.GetGuestServicePlanAsync(guestId, ct));

    // Guarded by the guest permission, not a service one: filling in a guest's
    // flight is guest work. Services.Manage governs the catalogue itself.
    [HttpPost]
    [HasPermission(PermissionCodes.GuestsUpdate)]
    public async Task<IActionResult> Save(Guid guestId, [FromBody] SaveGuestServiceEntryRequest request, CancellationToken ct)
        => ToResponse(await _catalog.SaveGuestServiceEntryAsync(guestId, request, _currentUser.UserId, ct));

    [HttpDelete("{entryId:guid}")]
    [HasPermission(PermissionCodes.GuestsUpdate)]
    public async Task<IActionResult> Delete(Guid guestId, Guid entryId, CancellationToken ct)
        => ToResponse(await _catalog.DeleteGuestServiceEntryAsync(guestId, entryId, _currentUser.UserId, ct));
}
