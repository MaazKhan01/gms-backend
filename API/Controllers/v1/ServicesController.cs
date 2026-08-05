using System;
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

// ============================================================================
// Per-event service catalog. Nested under the event because a Service only ever
// exists within one event — there is no global catalog (unlike VenueType /
// EventType, which are global lookups).
//
// GETs are open to any signed-in user, matching LookupController/Organizations:
// the Service Levels builder and the guest form both need to populate dropdowns
// without requiring the catalog-management permission.
// ============================================================================
[Route("api/v1/events/{eventId:guid}/services")]
[Authorize]
[ApiVersion("1.0")]
public class ServicesController(IServiceCatalogService _catalog, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetServices(Guid eventId, CancellationToken ct)
        => ToResponse(await _catalog.GetServicesAsync(eventId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.ServicesManage)]
    public async Task<IActionResult> CreateService(Guid eventId, [FromBody] CreateServiceRequest request, CancellationToken ct)
        => ToResponse(await _catalog.CreateServiceAsync(eventId, request, _currentUser.UserId, ct));

    [HttpPut("{serviceId:guid}")]
    [HasPermission(PermissionCodes.ServicesManage)]
    public async Task<IActionResult> UpdateService(Guid eventId, Guid serviceId, [FromBody] UpdateServiceRequest request, CancellationToken ct)
        => ToResponse(await _catalog.UpdateServiceAsync(eventId, serviceId, request, _currentUser.UserId, ct));

    [HttpDelete("{serviceId:guid}")]
    [HasPermission(PermissionCodes.ServicesManage)]
    public async Task<IActionResult> DeleteService(Guid eventId, Guid serviceId, CancellationToken ct)
        => ToResponse(await _catalog.DeleteServiceAsync(eventId, serviceId, _currentUser.UserId, ct));
}

// ============================================================================
// Per-event guest grades, built from the catalog above. Replaces the old
// hardcoded 6-value Guest.Tier list.
// ============================================================================
[Route("api/v1/events/{eventId:guid}/service-levels")]
[Authorize]
[ApiVersion("1.0")]
public class ServiceLevelsController(IServiceCatalogService _catalog, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetServiceLevels(Guid eventId, CancellationToken ct)
        => ToResponse(await _catalog.GetServiceLevelsAsync(eventId, ct));

    [HttpGet("{levelId:guid}")]
    public async Task<IActionResult> GetServiceLevel(Guid eventId, Guid levelId, CancellationToken ct)
        => ToResponse(await _catalog.GetServiceLevelByIdAsync(eventId, levelId, ct));

    /// <summary>Dry-run of the assignment rules — lets the guest form warn (and
    /// offer an override) before the user hits save.</summary>
    [HttpGet("{levelId:guid}/rule-check")]
    public async Task<IActionResult> CheckRules(Guid eventId, Guid levelId, [FromQuery] Guid? excludeGuestId, CancellationToken ct)
        => ToResponse(await _catalog.CheckRulesAsync(levelId, excludeGuestId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.ServiceLevelsManage)]
    public async Task<IActionResult> CreateServiceLevel(Guid eventId, [FromBody] CreateServiceLevelRequest request, CancellationToken ct)
        => ToResponse(await _catalog.CreateServiceLevelAsync(eventId, request, _currentUser.UserId, ct));

    [HttpPut("{levelId:guid}")]
    [HasPermission(PermissionCodes.ServiceLevelsManage)]
    public async Task<IActionResult> UpdateServiceLevel(Guid eventId, Guid levelId, [FromBody] UpdateServiceLevelRequest request, CancellationToken ct)
        => ToResponse(await _catalog.UpdateServiceLevelAsync(eventId, levelId, request, _currentUser.UserId, ct));

    [HttpDelete("{levelId:guid}")]
    [HasPermission(PermissionCodes.ServiceLevelsManage)]
    public async Task<IActionResult> DeleteServiceLevel(Guid eventId, Guid levelId, CancellationToken ct)
        => ToResponse(await _catalog.DeleteServiceLevelAsync(eventId, levelId, _currentUser.UserId, ct));
}
