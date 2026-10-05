using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Group;

namespace API.Controllers.v1;

/// <summary>Delegation sub-groups — admin-managed reference data.</summary>
[Route("api/v1/groups")]
[Authorize]
[ApiVersion("1.0")]
public class GroupsController(IGroupService _groups, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    // Open to any signed-in user, same as Departments: the group dropdown appears
    // on Nominations and on the transport booking form. Every write below is gated.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToResponse(await _groups.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await _groups.GetByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Lookups, AccessLevel.Write)]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request, CancellationToken ct)
        => ToResponse(await _groups.CreateAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Lookups, AccessLevel.Write)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupRequest request, CancellationToken ct)
        => ToResponse(await _groups.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Lookups, AccessLevel.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _groups.DeleteAsync(id, _currentUser.UserId, ct));
}
