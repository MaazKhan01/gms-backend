using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Guest;

namespace Core.Interfaces.Services;

public interface IGuestOverviewService
{
    // System-wide, paginated — EventId in the request is an optional filter,
    // not a scope: unlike IGuestService.GetGuestsAsync this lists across every event.
    Task<ApiResponse<PaginatedResponse<GuestOverviewRow>>> GetGuestOverviewAsync(GuestOverviewPagedRequest request, CancellationToken ct = default);

    // Fetched only when a row's accordion expands — keeps the list endpoint light.
    /// <summary>The person's whole cross-event history — <paramref name="personId"/>
    /// is a Guest.PublicId, not one of their participations.</summary>
    Task<ApiResponse<GuestOverviewDetailResponse>> GetGuestOverviewDetailAsync(Guid personId, CancellationToken ct = default);
}
