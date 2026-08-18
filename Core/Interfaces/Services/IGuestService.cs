using Core.ViewModel.Common;
using Core.ViewModel.Guest;

namespace Core.Interfaces.Services;

// Guest CRUD is EVENT-SCOPED: every `id` below is an EventGuest.PublicId — one
// person's participation in one event, which is what the guest screens edit.
// The person behind it is GuestResponse.PersonId (a Guest.PublicId), used by the
// cross-event overview, notifications and support chat.
public interface IGuestService
{
    /// <summary>Removes the participation. The person, their login and their other
    /// events survive.</summary>
    Task<ApiResponse<bool>> DeleteGuestByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<GuestResponse>> GetGuestByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<GuestResponse>>> GetGuestsAsync(Guid eventId, GuestPagedRequest request, CancellationToken ct = default);

    /// <summary>Name/org/tier only, for guest pickers (travel, meetings, seating).
    /// Server-side search + paging, declined guests always excluded.</summary>
    Task<ApiResponse<PaginatedResponse<GuestPickerResponse>>> GetGuestPickerAsync(Guid eventId, PagedRequest request, CancellationToken ct = default);

    /// <summary>"Existing Guest" tab of the Add Guest modal — people who already
    /// participate in a DIFFERENT event and are not yet on this one. POSTing
    /// /guest with the picked row's email reuses that master Guest and its login,
    /// adding a second participation rather than a second person.</summary>
    Task<ApiResponse<PaginatedResponse<OtherEventGuestRow>>> GetGuestsFromOtherEventsAsync(Guid currentEventId, PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<GuestResponse>> CreateGuestAsync(CreateGuestRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> BulkGuestsDeleteAsync(Guid eventId,DeleteMultipleGuests request, CancellationToken ct = default);
    // Bulk import — uploads the file to blob storage and enqueues a Hangfire job
    // (ProcessGuestsImportBatchAsync); the caller polls IImportBatchService.GetStatusAsync.
    Task<ApiResponse<StartImportResponse>> StartGuestsImportAsync(Guid eventId, Stream fileStream, string fileName, int createdBy, CancellationToken ct);
    Task ProcessGuestsImportBatchAsync(Guid batchId, CancellationToken ct = default);
    // The downloadable .xlsx template — dropdowns (Guest Type, Organization,
    // Nationality, Service Level) and the date columns' valid range are all
    // built from this event's own current data, not a static file.
    Task<byte[]> BuildGuestImportTemplateAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<GuestResponse>> UpdateGuestAsync( CreateGuestRequest request, CancellationToken ct);
    // Accreditation is granted for one event, so these take an EventGuest.PublicId.
    Task<ApiResponse<bool>> IssueAccreditationAsync(Guid eventGuestId, CancellationToken ct = default);
    Task<ApiResponse<bool>> RevokeAccreditationAsync(Guid eventGuestId, CancellationToken ct = default);
}
