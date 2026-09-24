using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Nomination;

namespace Core.Interfaces.Services;

public interface INominationService
{
    /// <summary>The mission's roster, with the derived flags.</summary>
    Task<ApiResponse<List<NominationResponse>>> GetRosterAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>People not yet on this mission, for the picker. Optionally
    /// narrowed to one department — which is how a Department Head is scoped.</summary>
    Task<ApiResponse<PaginatedResponse<NominationCandidateResponse>>> GetCandidatesAsync(
        Guid eventId, Guid? departmentId, PagedRequest request, bool includeOnRoster = false, CancellationToken ct = default);

    /// <summary>Roles a delegate may hold (Roles flagged IsDelegateRole).</summary>
    Task<ApiResponse<List<MissionRoleResponse>>> GetMissionRolesAsync(CancellationToken ct = default);

    Task<ApiResponse<NominationResponse>> NominateAsync(CreateNominationRequest request, int userId, CancellationToken ct = default);

    /// <summary>
    /// Adds a person to the staff directory, and nominates them straight away
    /// when the request names a mission. Without this there is no way into the
    /// directory at all: creating a guest always attaches them to an event.
    /// </summary>
    Task<ApiResponse<NominationResponse>> CreateStaffAsync(CreateStaffRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<NominationResponse>> UpdateAsync(Guid id, UpdateNominationRequest request, int userId, CancellationToken ct = default);

    /// <summary>Removes someone from the roster. Refused once they have bookings.</summary>
    Task<ApiResponse<bool>> RemoveAsync(Guid id, int userId, CancellationToken ct = default);

    // ── HR verification ──────────────────────────────────────────────────
    Task<ApiResponse<List<NominationResponse>>> GetForVerificationAsync(Guid eventId, string status, CancellationToken ct = default);
    Task<ApiResponse<HrVerificationResult>> VerifyAsync(HrVerifyRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<HrVerificationResult>> RejectAsync(HrRejectRequest request, int userId, CancellationToken ct = default);
}
