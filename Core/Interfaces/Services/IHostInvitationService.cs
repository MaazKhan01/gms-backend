using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Event;
using Core.ViewModel.HostInvitation;

namespace Core.Interfaces.Services;

public interface IHostInvitationService
{
    Task<ApiResponse<PaginatedResponse<HostInvitationResponse>>> GetAllAsync(
        PagedRequest request, string status, CancellationToken ct = default);

    Task<ApiResponse<HostInvitationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<HostInvitationResponse>> CreateAsync(CreateHostInvitationRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<HostInvitationResponse>> UpdateAsync(Guid id, UpdateHostInvitationRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);

    /// <summary>Declines an invitation without creating a mission.</summary>
    Task<ApiResponse<HostInvitationResponse>> DeclineAsync(Guid id, string reason, int userId, CancellationToken ct = default);

    /// <summary>Creates the mission this invitation becomes, and links the two.
    /// One mission per invitation — converting twice is rejected.</summary>
    Task<ApiResponse<EventResponse>> ConvertToMissionAsync(Guid id, ConvertInvitationRequest request, int userId, CancellationToken ct = default);
}
