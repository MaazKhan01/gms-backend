using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Incident;

namespace API.Controllers.v1;

/// <summary>
/// Phase 7 — help requests raised on the ground. Labelled "Help Requests" in the
/// UI; the code and table say Incident.
/// </summary>
[Route("api/v1/incidents")]
[Authorize]
[ApiVersion("1.0")]
public class IncidentsController(IIncidentService _incidents, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>A mission's incidents. All filters optional; open ones come first.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Incidents)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid eventId,
        [FromQuery] string status,
        [FromQuery] string severity,
        [FromQuery] string category,
        [FromQuery] Guid? delegateId,
        CancellationToken ct)
        => ToResponse(await _incidents.GetAsync(eventId, status, severity, category, delegateId, ct));

    [HttpGet("summary")]
    [HasPermission(PermissionCodes.Incidents)]
    public async Task<IActionResult> GetSummary([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _incidents.GetSummaryAsync(eventId, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Incidents)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await _incidents.GetByIdAsync(id, ct));

    /// <summary>Logs one. Raised via the portal — the delegate-facing app path
    /// records its own channel.</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.Incidents, AccessLevel.Write)]
    public async Task<IActionResult> Create([FromBody] CreateIncidentRequest request, CancellationToken ct)
        => ToResponse(await _incidents.CreateAsync(request, _currentUser.UserId, ct));

    /// <summary>Corrects the details. Status moves have their own endpoint.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Incidents, AccessLevel.Write)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIncidentRequest request, CancellationToken ct)
    {
        request ??= new UpdateIncidentRequest();
        request.Id = id;
        return ToResponse(await _incidents.UpdateAsync(request, _currentUser.UserId, ct));
    }

    /// <summary>Moves it along the track. Resolving requires a note.</summary>
    [HttpPost("{id:guid}/status")]
    [HasPermission(PermissionCodes.Incidents, AccessLevel.Write)]
    public async Task<IActionResult> ChangeStatus(
        Guid id, [FromBody] ChangeIncidentStatusRequest request, CancellationToken ct)
    {
        request ??= new ChangeIncidentStatusRequest();
        request.Id = id;
        return ToResponse(await _incidents.ChangeStatusAsync(request, _currentUser.UserId, ct));
    }

    /// <summary>For one logged by mistake — a real incident is resolved, not deleted.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Incidents, AccessLevel.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _incidents.DeleteAsync(id, _currentUser.UserId, ct));
}
