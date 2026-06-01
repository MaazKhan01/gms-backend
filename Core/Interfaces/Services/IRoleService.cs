using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Role;

namespace Core.Interfaces.Services;

public interface IRoleService
{
    Task<ApiResponse<RoleResponse>> CreateRoleAsync(CreateRoleRequest request, Guid currentUserId, CancellationToken ct = default);
    Task<ApiResponse<RoleResponse>> GetRoleByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<List<RoleResponse>>> GetAllRolesAsync(CancellationToken ct = default);
    Task<ApiResponse<RoleResponse>> UpdateRoleAsync(Guid id, UpdateRoleRequest request, Guid currentUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteRoleAsync(Guid id, CancellationToken ct = default);
}
