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
}
