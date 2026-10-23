using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Readiness;

namespace Core.Interfaces.Services;

public interface IReadinessService
{
    /// <summary>Per-delegate readiness for a mission. Optionally narrowed to
    /// those not yet travel-ready.</summary>
    Task<ApiResponse<List<ReadinessResponse>>> GetAsync(Guid eventId, bool onlyNotReady, CancellationToken ct = default);

    /// <summary>Mission-level counts for the screen header.</summary>
    Task<ApiResponse<ReadinessSummaryResponse>> GetSummaryAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>One participation's checklist — the delegate app's own view.
    /// Null when the participation doesn't exist.</summary>
    Task<ReadinessResponse> GetForParticipationAsync(int eventGuestId, CancellationToken ct = default);

    /// <summary>Overrides one red item with a reason.</summary>
    Task<ApiResponse<ReadinessResponse>> WaiveAsync(WaiveReadinessRequest request, int userId, CancellationToken ct = default);

    /// <summary>Withdraws a waiver, putting the item back on the checklist.</summary>
    Task<ApiResponse<ReadinessResponse>> RemoveWaiverAsync(Guid waiverId, int userId, CancellationToken ct = default);
}
