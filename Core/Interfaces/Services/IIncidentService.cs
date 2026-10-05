using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Incident;

namespace Core.Interfaces.Services;

public interface IIncidentService
{
    /// <summary>A mission's incidents, optionally narrowed. Open ones first, then
    /// newest.</summary>
    Task<ApiResponse<List<IncidentResponse>>> GetAsync(
        Guid eventId, string status, string severity, string category, Guid? delegateId,
        CancellationToken ct = default);

    Task<ApiResponse<IncidentResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Counts by status, severity and category for the screen header.</summary>
    Task<ApiResponse<IncidentSummaryResponse>> GetSummaryAsync(Guid eventId, CancellationToken ct = default);

    Task<ApiResponse<IncidentResponse>> CreateAsync(CreateIncidentRequest request, int userId, CancellationToken ct = default);

    Task<ApiResponse<IncidentResponse>> UpdateAsync(UpdateIncidentRequest request, int userId, CancellationToken ct = default);

    /// <summary>Moves it along the track. Resolving stamps who and when; moving
    /// back off resolved clears that stamp.</summary>
    Task<ApiResponse<IncidentResponse>> ChangeStatusAsync(
        ChangeIncidentStatusRequest request, int userId, CancellationToken ct = default);

    /// <summary>For an incident logged by mistake. Soft delete — a resolved
    /// incident is closed, not deleted.</summary>
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
