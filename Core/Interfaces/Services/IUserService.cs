using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.User;

namespace Core.Interfaces.Services;

public interface IUserService
{
    Task<ApiResponse<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<ApiResponse<UserResponse>> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<UserResponse>>> GetUsersAsync(PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<UserResponse>> UpdateUserAsync(Guid id, UpdateUserRequest request, int currentUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteUserAsync(Guid id, int currentUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    // Admin-initiated invites — replaces public self-registration.
    Task<ApiResponse<UserResponse>> InviteUserAsync(InviteUserRequest request, int inviterId, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<PendingUserResponse>>> GetPendingUsersAsync(PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResendInviteAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> AdminSetPasswordAsync(Guid id, AdminSetPasswordRequest request, CancellationToken ct = default);

    // Public accept-invite flow (tokenised link, no auth).
    Task<ApiResponse<InviteDetailsResponse>> GetInviteByTokenAsync(Guid token, CancellationToken ct = default);
    Task<ApiResponse<bool>> AcceptInviteAsync(Guid token, AcceptInviteRequest request, CancellationToken ct = default);
}
