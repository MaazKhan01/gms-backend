using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.UserAccess;

namespace API.Controllers.v1;

[Route("api/v1/user-access")]
[Authorize]
[ApiVersion("1.0")]
public class UserAccessController(IUserAccessService _service, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>Get the full module-access matrix for a user.</summary>
    [HttpGet("{userId:guid}")]
    [HasPermission(PermissionCodes.UserAccessManage)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
        => ToResponse(await _service.GetUserModuleAccessAsync(userId, ct));

    /// <summary>Replace the extra module grants for a user (full set, not patch).</summary>
    [HttpPut("{userId:guid}")]
    [HasPermission(PermissionCodes.UserAccessManage)]
    public async Task<IActionResult> Set(Guid userId, [FromBody] SetModuleAccessRequest request, CancellationToken ct)
        => ToResponse(await _service.SetUserModuleAccessAsync(userId, request, _currentUser.UserId, ct));
}
