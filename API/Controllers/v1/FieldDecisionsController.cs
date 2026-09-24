using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.FieldDecision;

namespace API.Controllers.v1;

/// <summary>
/// Phase 7 — the Head of Delegation's field decision log. Gated on the
/// head-of-delegation menu, which is where it lives in the UI.
/// </summary>
[Route("api/v1/field-decisions")]
[Authorize]
[ApiVersion("1.0")]
public class FieldDecisionsController(IFieldDecisionService _decisions, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>The mission's decision log, newest decision first.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.HeadOfDelegation)]
    public async Task<IActionResult> Get([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _decisions.GetAsync(eventId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.HeadOfDelegation, AccessLevel.Write)]
    public async Task<IActionResult> Create([FromBody] CreateFieldDecisionRequest request, CancellationToken ct)
        => ToResponse(await _decisions.CreateAsync(request, _currentUser.UserId, ct));

    /// <summary>Corrects what was typed. A decision that changed is a new entry.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.HeadOfDelegation, AccessLevel.Write)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateFieldDecisionRequest request, CancellationToken ct)
    {
        request ??= new UpdateFieldDecisionRequest();
        request.Id = id;
        return ToResponse(await _decisions.UpdateAsync(request, _currentUser.UserId, ct));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.HeadOfDelegation, AccessLevel.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _decisions.DeleteAsync(id, _currentUser.UserId, ct));
}
