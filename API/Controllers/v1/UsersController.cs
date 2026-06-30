using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.User;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class UsersController(IUserService _userService, ICurrentUser _currentUser) : Controllers.BaseApiController
{

    [HttpPost]
    [HasPermission(PermissionCodes.UsersCreate)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.CreateUserAsync(request, ct);
        if (result.Success)
            return CreatedAtAction(nameof(GetUserById), new { id = result.Data.Id }, result);
        return ToResponse(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.UsersView)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var result = await _userService.GetUserByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpGet]
    [HasPermission(PermissionCodes.UsersView)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string search = null,
        CancellationToken ct = default)
    {
        var result = await _userService.GetUsersAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize, SearchTerm = search }, ct);
        return ToResponse(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.UsersUpdate)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.UpdateUserAsync(id, request, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.UsersDelete)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var result = await _userService.DeleteUserAsync(id, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/change-password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (id != _currentUser.UserId && !User.HasClaim("permission", PermissionCodes.UsersUpdate))
            return Forbid();

        var result = await _userService.ChangePasswordAsync(id, request, ct);
        return ToResponse(result);
    }
}
