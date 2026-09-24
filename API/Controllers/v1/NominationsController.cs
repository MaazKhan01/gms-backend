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
using Core.ViewModel.Nomination;

namespace API.Controllers.v1;

/// <summary>Phase 2 — assembling a mission's delegation.</summary>
[Route("api/v1/nominations")]
[Authorize]
[ApiVersion("1.0")]
public class NominationsController(INominationService _nominations, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>The mission's roster, with passport and overlap warnings.</summary>
    [HttpGet]
    [HasPermission(AccessLevel.Read, PermissionCodes.Nominations, PermissionCodes.Guests)]
    public async Task<IActionResult> GetRoster([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _nominations.GetRosterAsync(eventId, ct));

    /// <summary>People not yet on this mission. Pass departmentId to scope the
    /// picker — that is how a Department Head is limited to their own staff.</summary>
    [HttpGet("candidates")]
    [HasPermission(PermissionCodes.Nominations)]
    public async Task<IActionResult> GetCandidates(
        [FromQuery] Guid eventId, [FromQuery] Guid? departmentId,
        [FromQuery] PagedRequest request, [FromQuery] bool includeOnRoster, CancellationToken ct)
        => ToResponse(await _nominations.GetCandidatesAsync(eventId, departmentId, request, includeOnRoster, ct));

    /// <summary>Roles a delegate may hold. Open to any signed-in user: it feeds a
    /// dropdown on the nomination form and nothing here is sensitive.</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetMissionRoles(CancellationToken ct)
        => ToResponse(await _nominations.GetMissionRolesAsync(ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Nominations, AccessLevel.Write)]
    public async Task<IActionResult> Nominate([FromBody] CreateNominationRequest request, CancellationToken ct)
        => ToResponse(await _nominations.NominateAsync(request, _currentUser.UserId, ct));

    /// <summary>
    /// Adds a person to the staff directory and nominates them in one step.
    /// The directory is the pool this screen draws from, and nothing else in
    /// DMS puts anyone into it.
    /// </summary>
    [HttpPost("staff")]
    [HasPermission(PermissionCodes.Nominations, AccessLevel.Write)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken ct)
        => ToResponse(await _nominations.CreateStaffAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Nominations, AccessLevel.Write)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNominationRequest request, CancellationToken ct)
        => ToResponse(await _nominations.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Nominations, AccessLevel.Write)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
        => ToResponse(await _nominations.RemoveAsync(id, _currentUser.UserId, ct));
}
