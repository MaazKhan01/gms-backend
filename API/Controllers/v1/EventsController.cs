using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Event;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class EventsController(IEventService _eventService, ICurrentUser _currentUser) : Controllers.BaseApiController
{

    [HttpGet]
    //[HasPermission(PermissionCodes.EventsView)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string search = null,
        [FromQuery] string status = null,
        CancellationToken ct = default)
    {
        var result = await _eventService.GetEventsAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize, SearchTerm = search }, status, ct);
        return ToResponse(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.EventsView)]
    public async Task<IActionResult> GetEventById(Guid id, CancellationToken ct)
        => ToResponse(await _eventService.GetEventByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.EventsCreate)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var result = await _eventService.CreateEventAsync(request, _currentUser.UserId, ct);
        if (result.Success)
            return CreatedAtAction(nameof(GetEventById), new { id = result.Data.Id }, result);
        return ToResponse(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.EventsUpdate)]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventRequest request, CancellationToken ct)
        => ToResponse(await _eventService.UpdateEventAsync(id, request, _currentUser.UserId, ct));

    [HttpPatch("{id:guid}/status")]
    [HasPermission(PermissionCodes.EventsManageStatus)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateEventStatusRequest request, CancellationToken ct)
        => ToResponse(await _eventService.UpdateStatusAsync(id, request.Status, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.EventsDelete)]
    public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken ct)
        => ToResponse(await _eventService.DeleteEventAsync(id, _currentUser.UserId, ct));

    // ── Sessions ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions(Guid id, CancellationToken ct)
        => ToResponse(await _eventService.GetSessionsAsync(id, ct));

    [HttpPost("{id:guid}/sessions")]
    [HasPermission(PermissionCodes.EventsManageSessions)]
    public async Task<IActionResult> AddSession(Guid id, [FromBody] CreateSessionRequest request, CancellationToken ct)
        => ToResponse(await _eventService.AddSessionAsync(id, request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}/sessions/{sessionId:guid}")]
    [HasPermission(PermissionCodes.EventsManageSessions)]
    public async Task<IActionResult> UpdateSession(Guid id, Guid sessionId, [FromBody] UpdateSessionRequest request, CancellationToken ct)
        => ToResponse(await _eventService.UpdateSessionAsync(id, sessionId, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}/sessions/{sessionId:guid}")]
    [HasPermission(PermissionCodes.EventsManageSessions)]
    public async Task<IActionResult> DeleteSession(Guid id, Guid sessionId, CancellationToken ct)
        => ToResponse(await _eventService.DeleteSessionAsync(id, sessionId, _currentUser.UserId, ct));
}
