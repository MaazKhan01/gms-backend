using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Permission;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class PermissionsController : Controllers.BaseApiController
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;

    public PermissionsController(IPermissionService permissionService, ICurrentUser currentUser)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPermissions([FromQuery] string module, CancellationToken ct)
    {
        var result = string.IsNullOrWhiteSpace(module)
            ? await _permissionService.GetAllPermissionsAsync(ct)
            : await _permissionService.GetPermissionsByModuleAsync(module, ct);
        return ToResponse(result);
    }

    [HttpGet("modules")]
    public async Task<IActionResult> GetModules(CancellationToken ct)
    {
        var result = await _permissionService.GetModulesAsync(ct);
        return ToResponse(result);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.RolesManage)]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request, CancellationToken ct)
    {
        var result = await _permissionService.CreatePermissionAsync(request, _currentUser.UserId, ct);
        if (result.Success)
            return CreatedAtAction(nameof(GetPermissions), null, result);
        return ToResponse(result);
    }
}
