using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Reports;

namespace Core.Interfaces.Services;

/// <summary>
/// Phase 9 — Reports &amp; Close. Delegates' own reports, the combined mission
/// report assembled from them, and the close that follows publication.
/// </summary>
public interface IMissionReportService
{
    Task<ApiResponse<List<PostMissionReportResponse>>> GetReportsAsync(Guid eventId, string status, CancellationToken ct = default);
    Task<ApiResponse<PostMissionReportSummary>> GetReportSummaryAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<PostMissionReportResponse>> SaveReportAsync(SaveReportRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<PostMissionReportResponse>> ApproveReportAsync(Guid participationId, int userId, CancellationToken ct = default);
    Task<ApiResponse<NudgeResult>> NudgeAsync(NudgeReportsRequest request, int userId, CancellationToken ct = default);

    Task<ApiResponse<CombinedReportResponse>> GetCombinedAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<CombinedReportResponse>> AssembleAsync(CombinedReportActionRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<CombinedReportResponse>> SaveCombinedAsync(SaveCombinedReportRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<CombinedReportResponse>> SubmitForApprovalAsync(CombinedReportActionRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<CombinedReportResponse>> ApproveCombinedAsync(CombinedReportActionRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<CombinedReportResponse>> PublishAsync(CombinedReportActionRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<CombinedReportResponse>> CloseMissionAsync(CombinedReportActionRequest request, int userId, CancellationToken ct = default);
}
