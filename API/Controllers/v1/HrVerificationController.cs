using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Nomination;

namespace API.Controllers.v1;

/// <summary>
/// Phase 2 — HR's sign-off on the roster. A nomination lands as pending; HR
/// either confirms the passport, grade, visa and insurance records or sends it
/// back with a note.
///
/// Verify and reject are both bulk: HR works through a list, so a single id is
/// just a batch of one.
/// </summary>
[Route("api/v1/hr-verification")]
[Authorize]
[ApiVersion("1.0")]
public class HrVerificationController(INominationService _nominations, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>The roster with HR status, optionally filtered to
    /// pending / verified / rejected.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.HrVerification)]
    public async Task<IActionResult> GetAll([FromQuery] Guid eventId, [FromQuery] string status, CancellationToken ct)
        => ToResponse(await _nominations.GetForVerificationAsync(eventId, status, ct));

    [HttpPost("verify")]
    [HasPermission(PermissionCodes.HrVerification, AccessLevel.Write)]
    public async Task<IActionResult> Verify([FromBody] HrVerifyRequest request, CancellationToken ct)
        => ToResponse(await _nominations.VerifyAsync(request, _currentUser.UserId, ct));

    /// <summary>Sends nominations back to the coordinator. A reason is required.</summary>
    [HttpPost("reject")]
    [HasPermission(PermissionCodes.HrVerification, AccessLevel.Write)]
    public async Task<IActionResult> Reject([FromBody] HrRejectRequest request, CancellationToken ct)
        => ToResponse(await _nominations.RejectAsync(request, _currentUser.UserId, ct));

    /// <summary>
    /// Withdraws a decision, putting the nomination back to pending. Documents
    /// change after HR has looked at them — a renewed passport, a visa that
    /// came through, a rejection entered against the wrong row — so sign-off
    /// has to be reversible rather than only correctable by re-nominating.
    /// </summary>
    [HttpPost("revert")]
    [HasPermission(PermissionCodes.HrVerification, AccessLevel.Write)]
    public async Task<IActionResult> Revert([FromBody] HrRevertRequest request, CancellationToken ct)
        => ToResponse(await _nominations.RevertVerificationAsync(request, _currentUser.UserId, ct));
}
