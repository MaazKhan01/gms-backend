using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Role;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class RolesController : Controllers.BaseApiController
{
    private readonly IRoleService _roleService;
    private readonly ICurrentUser _currentUser;

    public RolesController(IRoleService roleService, ICurrentUser currentUser)
    {
        _roleService = roleService;
        _currentUser = currentUser;
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Roles, AccessLevel.Write)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var result = await _roleService.CreateRoleAsync(request, _currentUser.UserId, ct);
        if (result.Success)
            return CreatedAtAction(nameof(GetRoleById), new { id = result.Data.Id }, result);
        return ToResponse(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Roles)]
    public async Task<IActionResult> GetRoleById(Guid id, CancellationToken ct)
    {
        var result = await _roleService.GetRoleByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Roles)]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
    {
        var result = await _roleService.GetAllRolesAsync(ct);
        return ToResponse(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Roles, AccessLevel.Write)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        var result = await _roleService.UpdateRoleAsync(id, request, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Roles, AccessLevel.Write)]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct)
    {
        var result = await _roleService.DeleteRoleAsync(id, ct);
        return ToResponse(result);
    }
}
