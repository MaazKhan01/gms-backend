using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
public class EventsController(IEventService _eventService, IImportBatchService _importBatchService, ICurrentUser _currentUser) : Controllers.BaseApiController
{

    [HttpGet]
    // Deliberately open to any signed-in user, not gated on Events.
    // The mission switcher sits in the app chrome on every screen, and every
    // mission-scoped module needs the list to resolve the active mission — so
    // gating this would break all of them for anyone without the Missions menu.
    // Reading the list is not sensitive; every write below is gated.
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
    [HasPermission(PermissionCodes.Events)]
    public async Task<IActionResult> GetEventById(Guid id, CancellationToken ct)
        => ToResponse(await _eventService.GetEventByIdAsync(id, ct));

    [HttpGet("types")]
    public async Task<IActionResult> GetEventTypes(CancellationToken ct)
        => ToResponse(await _eventService.GetEventTypesAsync(ct));

    [HttpPost("types")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> CreateEventType([FromBody] CreateEventTypeRequest request, CancellationToken ct)
        => ToResponse(await _eventService.CreateEventTypeAsync(request, _currentUser.UserId, ct));

    [HttpGet("import-template")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> GetImportTemplate(CancellationToken ct)
    {
        var bytes = await _eventService.BuildImportTemplateAsync(ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "events-import-template.xlsx");
    }

    [HttpPost("import")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> ImportEvents(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("A file is required."));

        using var stream = file.OpenReadStream();
        var result = await _eventService.StartEventsImportAsync(stream, file.FileName, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    // Polled by the frontend after StartEventsImportAsync returns — the actual
    // import runs as a Hangfire job, so the user never waits on this request.
    [HttpGet("import/{batchId:guid}")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> GetImportBatchStatus(Guid batchId, CancellationToken ct)
        => ToResponse(await _importBatchService.GetStatusAsync(batchId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var result = await _eventService.CreateEventAsync(request, _currentUser.UserId, ct);
        if (result.Success)
            return CreatedAtAction(nameof(GetEventById), new { id = result.Data.Id }, result);
        return ToResponse(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventRequest request, CancellationToken ct)
        => ToResponse(await _eventService.UpdateEventAsync(id, request, _currentUser.UserId, ct));


    [HttpPut("{id:guid}/status")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateEventStatusRequest request, CancellationToken ct)
        => ToResponse(await _eventService.UpdateStatusAsync(id, request.Status, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken ct)
        => ToResponse(await _eventService.DeleteEventAsync(id, _currentUser.UserId, ct));

    // ── Sessions ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions(Guid id, CancellationToken ct)
        => ToResponse(await _eventService.GetSessionsAsync(id, ct));

    [HttpPost("{id:guid}/sessions")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> AddSession(Guid id, [FromBody] CreateSessionRequest request, CancellationToken ct)
        => ToResponse(await _eventService.AddSessionAsync(id, request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}/sessions/{sessionId:guid}")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> UpdateSession(Guid id, Guid sessionId, [FromBody] UpdateSessionRequest request, CancellationToken ct)
        => ToResponse(await _eventService.UpdateSessionAsync(id, sessionId, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}/sessions/{sessionId:guid}")]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> DeleteSession(Guid id, Guid sessionId, CancellationToken ct)
        => ToResponse(await _eventService.DeleteSessionAsync(id, sessionId, _currentUser.UserId, ct));
}
