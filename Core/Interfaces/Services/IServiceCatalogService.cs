using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.ServiceCatalog;

namespace Core.Interfaces.Services;

/// <summary>
/// Per-event service catalog and the guest grades ("Service Levels") built from
/// it. Everything here is scoped by the event's public id — there is no global
/// catalog, by design.
/// </summary>
public interface IServiceCatalogService
{
    // ── Services ─────────────────────────────────────────────────────────────
    Task<ApiResponse<List<ServiceResponse>>> GetServicesAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<ServiceResponse>> CreateServiceAsync(Guid eventId, CreateServiceRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<ServiceResponse>> UpdateServiceAsync(Guid eventId, Guid serviceId, UpdateServiceRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteServiceAsync(Guid eventId, Guid serviceId, int userId, CancellationToken ct = default);

    // ── Service levels ───────────────────────────────────────────────────────
    Task<ApiResponse<List<ServiceLevelResponse>>> GetServiceLevelsAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<ServiceLevelResponse>> GetServiceLevelByIdAsync(Guid eventId, Guid levelId, CancellationToken ct = default);
    Task<ApiResponse<ServiceLevelResponse>> CreateServiceLevelAsync(Guid eventId, CreateServiceLevelRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<ServiceLevelResponse>> UpdateServiceLevelAsync(Guid eventId, Guid levelId, UpdateServiceLevelRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteServiceLevelAsync(Guid eventId, Guid levelId, int userId, CancellationToken ct = default);

    /// <summary>Dry-run of the assignment rules, so the guest form can warn before
    /// submitting. <paramref name="excludeGuestId"/> keeps a guest already on the
    /// level from counting against its own capacity on edit.</summary>
    Task<ApiResponse<ServiceLevelRuleCheckResponse>> CheckRulesAsync(
        Guid levelId, Guid? excludeGuestId, CancellationToken ct = default);
}
