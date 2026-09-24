using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.RoleAccess;

namespace API.Controllers.v1;

/// <summary>
/// Role Access — read/write permissions per role, per menu. This replaces the
/// old per-user "User Access" module grants: access is a property of the ROLE,
/// never of the individual user.
/// </summary>
[Route("api/v1/role-access")]
[Authorize]
[ApiVersion("1.0")]
public class RoleAccessController(IRoleAccessService _service, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>
    /// The signed-in user's own navigation tree and read/write flags. Every
    /// authenticated user may call this — it only ever describes their own role.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => ToResponse(await _service.GetMyAccessAsync(ct));

    /// <summary>The full menu tree, no role attached.</summary>
    [HttpGet("permissions")]
    [HasPermission(PermissionCodes.RoleAccess)]
    public async Task<IActionResult> Permissions(CancellationToken ct)
        => ToResponse(await _service.GetPermissionTreeAsync(ct));

    /// <summary>Every menu/submenu with this role's Read/Write flags.</summary>
    [HttpGet("{roleId:guid}")]
    [HasPermission(PermissionCodes.RoleAccess)]
    public async Task<IActionResult> Get(Guid roleId, CancellationToken ct)
        => ToResponse(await _service.GetRoleAccessAsync(roleId, ct));

    /// <summary>Replaces this role's access in full (not a patch).</summary>
    [HttpPut("{roleId:guid}")]
    [HasPermission(PermissionCodes.RoleAccess, AccessLevel.Write)]
    public async Task<IActionResult> Set(Guid roleId, [FromBody] SetRoleAccessRequest request, CancellationToken ct)
        => ToResponse(await _service.SetRoleAccessAsync(roleId, request, _currentUser.UserId, ct));
}
