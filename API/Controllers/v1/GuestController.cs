using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Guest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class GuestController(IGuestService _guestService, IImportBatchService _importBatchService, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // ── Frontend contract ────────────────────────────────────────────────────
    // Every {id} on this controller is an EventGuest.PublicId: one person's
    // participation in one event, which is what these screens create, edit,
    // accredit and remove. It is what GuestResponse.id returns, and what the
    // travel / seating / meetings / transport endpoints take.
    //
    // The person behind it is GuestResponse.personId (a Guest.PublicId) — stable
    // across every event they attend, and what the guest-overview detail,
    // notifications and support chat take instead.
    //
    // Adding someone to a second event is just POST with their existing email:
    // the master Guest and their login are reused and only the participation is
    // new. Posting an email that is already on THIS event is a 409
    // (GUEST_ALREADY_ON_EVENT). Email cannot be changed on PUT.

    // Any-of: the delegate roster is read by half the portal — Accreditation,
    // Seating, Meetings, Services, Transportation, Readiness, Nominations and HR
    // Verification all need it. Gating on Delegates alone broke those screens for
    // roles that legitimately hold them, which is why this was commented out.
    // Still gated, just on "holds any menu that reads the roster" rather than
    // being open to every signed-in user.
    [HttpGet]
    [HasPermission(AccessLevel.Read,
        PermissionCodes.Guests, PermissionCodes.Nominations, PermissionCodes.HrVerification,
        PermissionCodes.Accreditation, PermissionCodes.Seating, PermissionCodes.Meetings,
        PermissionCodes.Services, PermissionCodes.Transportation, PermissionCodes.Readiness)]
    public async Task<IActionResult> GetGuests([FromQuery] Guid eventId, [FromQuery] GuestPagedRequest request, CancellationToken ct)
    {
        var result = await _guestService.GetGuestsAsync(eventId, request, ct);
        return ToResponse(result);
    }

    // Lightweight feed for guest pickers — name/org/tier/photo only, searched and
    // paged server-side. The full GET above is for the guest table.
    [HttpGet("picker")]
    public async Task<IActionResult> GetGuestPicker([FromQuery] Guid eventId, [FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _guestService.GetGuestPickerAsync(eventId, request, ct));

    // "Existing Guest" tab of the Add Guest modal — guests from every OTHER
    // event, so one can be picked to prefill a brand-new guest for this event.
    // Feeds the "add an existing person" picker, so Nominations needs it too.
    [HttpGet("other-events")]
    [HasPermission(AccessLevel.Read, PermissionCodes.Guests, PermissionCodes.Nominations)]
    public async Task<IActionResult> GetGuestsFromOtherEvents([FromQuery] Guid currentEventId, [FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _guestService.GetGuestsFromOtherEventsAsync(currentEventId, request, ct));

    // Same any-of set as the list above: a screen that may show the roster may
    // open one row of it.
    [HttpGet("{id:guid}")]
    [HasPermission(AccessLevel.Read,
        PermissionCodes.Guests, PermissionCodes.Nominations, PermissionCodes.HrVerification,
        PermissionCodes.Accreditation, PermissionCodes.Seating, PermissionCodes.Meetings,
        PermissionCodes.Services, PermissionCodes.Transportation, PermissionCodes.Readiness)]
    public async Task<IActionResult> GetGuestById(Guid id, CancellationToken ct)
    {
        var result = await _guestService.GetGuestByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> CreateGuest([FromBody] CreateGuestRequest request, CancellationToken ct)
    {
        // Email is required for manually-created guests only — CSV import
        // (ImportGuestCsvAsync) calls the service directly and stays permissive.
        if (string.IsNullOrWhiteSpace(request.Email))
            return ToResponse(ApiResponse<GuestResponse>.ErrorResponse("Email is required"));

        var result = await _guestService.CreateGuestAsync(request, ct);
        if (result.Success)
            return CreatedAtAction(nameof(GetGuestById), new { id = result.Data.Id }, result);
        return ToResponse(result);
    }

    [HttpPost("import")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportGuestCsv([FromQuery] Guid eventId, IFormFile file, CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            return Ok(ApiResponse<object>.ErrorResponse("eventId is required"));

        if (file == null || file.Length == 0)
            return Ok(ApiResponse<object>.ErrorResponse("Please upload a file"));

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Ok(ApiResponse<object>.ErrorResponse("Only the .xlsx import template is allowed — download a fresh one from this dialog."));

        using var stream = file.OpenReadStream();
        var result = await _guestService.StartGuestsImportAsync(eventId, stream, file.FileName, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    // Polled by the frontend after StartGuestsImportAsync returns — the actual
    // import runs as a Hangfire job, so the user never waits on this request.
    [HttpGet("import/{batchId:guid}")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> GetImportBatchStatus(Guid batchId, CancellationToken ct)
        => ToResponse(await _importBatchService.GetStatusAsync(batchId, ct));

    // The downloadable .xlsx template — dropdowns and the date columns' valid
    // range are built from this event's own current data every time it's
    // exported, so it can never go stale the way a static file would.
    [HttpGet("import-template")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> GetImportTemplate([FromQuery] Guid eventId, CancellationToken ct)
    {
        var bytes = await _guestService.BuildGuestImportTemplateAsync(eventId, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "guest-import-template.xlsx");
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> DeleteGuestById(Guid id, CancellationToken ct = default)
    {
        var result = await _guestService.DeleteGuestByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpDelete("delete")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> DeleteSelected([FromQuery] Guid eventId, [FromBody] DeleteMultipleGuests request, CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("eventId is required"));

        var result = await _guestService.BulkGuestsDeleteAsync(eventId, request, ct);
        return ToResponse(result);
    }
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> UpdateGuest(Guid id, [FromBody] CreateGuestRequest request, CancellationToken ct)
    {
        request.Id = id;
        var result = await _guestService.UpdateGuestAsync(request, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/accreditation/issue")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> IssueAccreditation(Guid id, CancellationToken ct)
    {
        var result = await _guestService.IssueAccreditationAsync(id, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/accreditation/revoke")]
    [HasPermission(PermissionCodes.Guests, AccessLevel.Write)]
    public async Task<IActionResult> RevokeAccreditation(Guid id, CancellationToken ct)
    {
        var result = await _guestService.RevokeAccreditationAsync(id, ct);
        return ToResponse(result);
    }

}
