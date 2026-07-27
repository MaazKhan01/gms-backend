using Core.ViewModel.Common;
using Core.ViewModel.Guest;

namespace Core.Interfaces.Services;

public interface IGuestService
{
    Task<ApiResponse<bool>> DeleteGuestByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<GuestResponse>> GetGuestByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<GuestResponse>>> GetGuestsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<GuestResponse>> CreateGuestAsync(CreateGuestRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> BulkGuestsDeleteAsync(Guid eventId,DeleteMultipleGuests request, CancellationToken ct = default);
    Task<ApiResponse<ImportGuestsResult>> ImportGuestCsvAsync(Guid eventId, Stream csvStream, int createdBy, CancellationToken ct);
    Task<ApiResponse<GuestResponse>> UpdateGuestAsync( CreateGuestRequest request, CancellationToken ct);
    Task<ApiResponse<bool>> IssueAccreditationAsync(Guid guestId, CancellationToken ct = default);
    Task<ApiResponse<bool>> RevokeAccreditationAsync(Guid guestId, CancellationToken ct = default);
}
