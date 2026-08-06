using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.ServiceCatalog;

namespace Core.Interfaces.Services;

/// <summary>
/// The global service catalogue and each guest's service plan.
/// See docs/service-levels-v2.md.
/// </summary>
public interface IServiceCatalogService
{
    // ── Services (global) ────────────────────────────────────────────────────
    Task<ApiResponse<List<ServiceResponse>>> GetServicesAsync(bool includeInactive, CancellationToken ct = default);
    Task<ApiResponse<ServiceResponse>> GetServiceByIdAsync(Guid serviceId, CancellationToken ct = default);
    Task<ApiResponse<ServiceResponse>> CreateServiceAsync(CreateServiceRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<ServiceResponse>> UpdateServiceAsync(Guid serviceId, UpdateServiceRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteServiceAsync(Guid serviceId, int userId, CancellationToken ct = default);

    // ── Service levels (global) ──────────────────────────────────────────────
    Task<ApiResponse<List<ServiceLevelResponse>>> GetServiceLevelsAsync(bool includeInactive, CancellationToken ct = default);
    Task<ApiResponse<ServiceLevelResponse>> GetServiceLevelByIdAsync(Guid levelId, CancellationToken ct = default);
    Task<ApiResponse<ServiceLevelResponse>> CreateServiceLevelAsync(CreateServiceLevelRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<ServiceLevelResponse>> UpdateServiceLevelAsync(Guid levelId, UpdateServiceLevelRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteServiceLevelAsync(Guid levelId, int userId, CancellationToken ct = default);

    // ── A guest's service plan ───────────────────────────────────────────────

    /// <summary>
    /// The guest's checklist: every service on their level, what has been filled
    /// in, and which are unlocked given the event's guest model.
    /// </summary>
    Task<ApiResponse<GuestServicePlanResponse>> GetGuestServicePlanAsync(Guid guestId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates one entry. Rejects a service the guest's level does not
    /// include, and on a Fixed event one whose predecessors are still pending.
    /// </summary>
    Task<ApiResponse<GuestServiceEntryResponse>> SaveGuestServiceEntryAsync(
        Guid guestId, SaveGuestServiceEntryRequest request, int userId, CancellationToken ct = default);

    Task<ApiResponse<bool>> DeleteGuestServiceEntryAsync(Guid guestId, Guid entryId, int userId, CancellationToken ct = default);

    /// <summary>
    /// Every entry for one service across an event — what the operational
    /// listings (formerly Travel &amp; Logistics) render.
    /// </summary>
    Task<ApiResponse<PaginatedResponse<ServiceEntryRow>>> GetServiceEntriesAsync(
        Guid serviceId, Guid eventId, PagedRequest request, CancellationToken ct = default);
}
