using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.RoleAccess;

namespace Core.Interfaces.Services;

public interface IRoleAccessService
{
    /// <summary>The whole permission tree, no role attached — for pickers and diagnostics.</summary>
    Task<ApiResponse<List<PermissionNode>>> GetPermissionTreeAsync(CancellationToken ct = default);

    /// <summary>Every permission with one role's Read/Write flags — powers the Role Access admin screen.</summary>
    Task<ApiResponse<RoleAccessResponse>> GetRoleAccessAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>Replaces a role's access in full.</summary>
    Task<ApiResponse<RoleAccessResponse>> SetRoleAccessAsync(Guid roleId, SetRoleAccessRequest request, int adminId, CancellationToken ct = default);

    /// <summary>The signed-in user's own pruned navigation tree + flags.</summary>
    Task<ApiResponse<MyAccessResponse>> GetMyAccessAsync(CancellationToken ct = default);

    /// <summary>
    /// (code → read, write) for one role, straight from the DB. Used when the
    /// access token is minted, so the claims and this API can never disagree.
    /// </summary>
    Task<Dictionary<string, (bool Read, bool Write)>> GetRoleAccessMapAsync(int roleId, CancellationToken ct = default);
}
