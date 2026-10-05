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
    [HasPermission(PermissionCodes.Users, AccessLevel.Write)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.CreateUserAsync(request, ct);
        if (result.Success)
            return ToResponse( result);
        return ToResponse(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Users)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var result = await _userService.GetUserByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Users)]
    /// <summary><paramref name="audience"/>: platform | delegates. Omitted,
    /// both populations are listed together.</summary>
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string search = null,
        [FromQuery] string audience = null,
        CancellationToken ct = default)
    {
        var result = await _userService.GetUsersAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize, SearchTerm = search }, audience, ct);
        return ToResponse(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Users, AccessLevel.Write)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.UpdateUserAsync(id, request, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Users, AccessLevel.Write)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var result = await _userService.DeleteUserAsync(id, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/change-password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        // Anyone may change their OWN password; changing someone else's needs
        // write on Users. Goes through ICurrentUser so this and [HasPermission]
        // share one evaluator rather than hand-reading a claim.
        if (id != _currentUser.UserPublicId && !_currentUser.CanWrite(PermissionCodes.Users))
            return Forbid();

        var result = await _userService.ChangePasswordAsync(id, request, ct);
        return ToResponse(result);
    }

    // ── Admin-initiated invites ──────────────────────────────────────────────
    [HttpPost("invite")]
    [HasPermission(PermissionCodes.Users, AccessLevel.Write)]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        var result = await _userService.InviteUserAsync(request, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpGet("pending")]
    [HasPermission(PermissionCodes.Users)]
    public async Task<IActionResult> GetPendingUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string search = null,
        CancellationToken ct = default)
    {
        var result = await _userService.GetPendingUsersAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize, SearchTerm = search }, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/resend-invite")]
    [HasPermission(PermissionCodes.Users, AccessLevel.Write)]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken ct)
    {
        var result = await _userService.ResendInviteAsync(id, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/admin-set-password")]
    [HasPermission(PermissionCodes.Users, AccessLevel.Write)]
    public async Task<IActionResult> AdminSetPassword(Guid id, [FromBody] AdminSetPasswordRequest request, CancellationToken ct)
    {
        var result = await _userService.AdminSetPasswordAsync(id, request, ct);
        return ToResponse(result);
    }
}
