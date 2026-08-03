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
    [HttpGet]
    //[HasPermission(PermissionCodes.GuestsView)]
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

    [HttpGet("{id:guid}")]
    //[HasPermission(PermissionCodes.GuestsView)]
    public async Task<IActionResult> GetGuestById(Guid id, CancellationToken ct)
    {
        var result = await _guestService.GetGuestByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.GuestsCreate)]
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
    [HasPermission(PermissionCodes.GuestsImport)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportGuestCsv([FromQuery] Guid eventId, IFormFile file, CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            return Ok(ApiResponse<object>.ErrorResponse("eventId is required"));

        if (file == null || file.Length == 0)
            return Ok(ApiResponse<object>.ErrorResponse("Please upload a file"));

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return Ok(ApiResponse<object>.ErrorResponse("Only CSV files are allowed"));

        using var stream = file.OpenReadStream();
        var result = await _guestService.StartGuestsImportAsync(eventId, stream, file.FileName, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    // Polled by the frontend after StartGuestsImportAsync returns — the actual
    // import runs as a Hangfire job, so the user never waits on this request.
    [HttpGet("import/{batchId:guid}")]
    [HasPermission(PermissionCodes.GuestsImport)]
    public async Task<IActionResult> GetImportBatchStatus(Guid batchId, CancellationToken ct)
        => ToResponse(await _importBatchService.GetStatusAsync(batchId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.GuestsDelete)]
    public async Task<IActionResult> DeleteGuestById(Guid id, CancellationToken ct = default)
    {
        var result = await _guestService.DeleteGuestByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpDelete("delete")]
    [HasPermission(PermissionCodes.GuestsDelete)]
    public async Task<IActionResult> DeleteSelected([FromQuery] Guid eventId, [FromBody] DeleteMultipleGuests request, CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("eventId is required"));

        var result = await _guestService.BulkGuestsDeleteAsync(eventId, request, ct);
        return ToResponse(result);
    }
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.GuestsUpdate)]
    public async Task<IActionResult> UpdateGuest(Guid id, [FromBody] CreateGuestRequest request, CancellationToken ct)
    {
        request.Id = id;
        var result = await _guestService.UpdateGuestAsync(request, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/accreditation/issue")]
    [HasPermission(PermissionCodes.GuestsUpdate)]
    public async Task<IActionResult> IssueAccreditation(Guid id, CancellationToken ct)
    {
        var result = await _guestService.IssueAccreditationAsync(id, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/accreditation/revoke")]
    [HasPermission(PermissionCodes.GuestsUpdate)]
    public async Task<IActionResult> RevokeAccreditation(Guid id, CancellationToken ct)
    {
        var result = await _guestService.RevokeAccreditationAsync(id, ct);
        return ToResponse(result);
    }

}
