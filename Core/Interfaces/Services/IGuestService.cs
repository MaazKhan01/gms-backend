using Core.ViewModel.Common;
using Core.ViewModel.Guest;

namespace Core.Interfaces.Services;

public interface IGuestService
{
    Task<ApiResponse<bool>> DeleteGuestByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<GuestResponse>> GetGuestByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<GuestResponse>>> GetGuestsAsync(Guid eventId, GuestPagedRequest request, CancellationToken ct = default);

    /// <summary>Name/org/tier only, for guest pickers (travel, meetings, seating).
    /// Server-side search + paging, declined guests always excluded.</summary>
    Task<ApiResponse<PaginatedResponse<GuestPickerResponse>>> GetGuestPickerAsync(Guid eventId, PagedRequest request, CancellationToken ct = default);

    /// <summary>"Existing Guest" tab of the Add Guest modal — guests that already
    /// exist under a DIFFERENT event, so one can be picked to prefill a brand-new
    /// guest for the current event (Guest has no cross-event identity to link to).</summary>
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
    Task<ApiResponse<bool>> IssueAccreditationAsync(Guid guestId, CancellationToken ct = default);
    Task<ApiResponse<bool>> RevokeAccreditationAsync(Guid guestId, CancellationToken ct = default);
}
