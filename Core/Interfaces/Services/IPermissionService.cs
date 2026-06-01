using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Permission;

namespace Core.Interfaces.Services;

public interface IPermissionService
{
    Task<ApiResponse<List<PermissionResponse>>> GetAllPermissionsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<PermissionResponse>>> GetPermissionsByModuleAsync(string module, CancellationToken ct = default);
    Task<ApiResponse<List<string>>> GetModulesAsync(CancellationToken ct = default);
    Task<ApiResponse<PermissionResponse>> CreatePermissionAsync(CreatePermissionRequest request, Guid currentUserId, CancellationToken ct = default);
}
