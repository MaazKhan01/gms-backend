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
using Core.ViewModel.HostInvitation;

namespace API.Controllers.v1;

/// <summary>
/// Phase 1 — invitations received from host organisations, and converting one
/// into a mission.
///
/// Not to be confused with <c>InvitationController</c>, which is the outbound
/// RSVP sent to a delegate. These are inbound and unrelated.
/// </summary>
[Route("api/v1/external-invitations")]
[Authorize]
[ApiVersion("1.0")]
public class HostInvitationsController(IHostInvitationService _invitations, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>Paged list, optionally filtered by status (logged/converted/declined).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.ExternalInvitations)]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, [FromQuery] string status, CancellationToken ct)
        => ToResponse(await _invitations.GetAllAsync(request, status, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.ExternalInvitations)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await _invitations.GetByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.ExternalInvitations, AccessLevel.Write)]
    public async Task<IActionResult> Create([FromBody] CreateHostInvitationRequest request, CancellationToken ct)
        => ToResponse(await _invitations.CreateAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.ExternalInvitations, AccessLevel.Write)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHostInvitationRequest request, CancellationToken ct)
        => ToResponse(await _invitations.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.ExternalInvitations, AccessLevel.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _invitations.DeleteAsync(id, _currentUser.UserId, ct));

    /// <summary>Declines the invitation. No mission is created.</summary>
    [HttpPost("{id:guid}/decline")]
    [HasPermission(PermissionCodes.ExternalInvitations, AccessLevel.Write)]
    public async Task<IActionResult> Decline(Guid id, [FromBody] DeclineInvitationRequest request, CancellationToken ct)
        => ToResponse(await _invitations.DeclineAsync(id, request?.Reason, _currentUser.UserId, ct));

    /// <summary>
    /// Creates the mission this invitation becomes. Needs write on Missions too —
    /// this is the one endpoint that creates one from outside the Missions module.
    /// </summary>
    [HttpPost("{id:guid}/convert")]
    [HasPermission(PermissionCodes.ExternalInvitations, AccessLevel.Write)]
    [HasPermission(PermissionCodes.Events, AccessLevel.Write)]
    public async Task<IActionResult> Convert(Guid id, [FromBody] ConvertInvitationRequest request, CancellationToken ct)
        => ToResponse(await _invitations.ConvertToMissionAsync(id, request, _currentUser.UserId, ct));
}

public class DeclineInvitationRequest
{
    public string Reason { get; set; }
}
