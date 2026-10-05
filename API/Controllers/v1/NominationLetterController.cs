using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.NominationLetter;

namespace API.Controllers.v1;

/// <summary>
/// Phase 4 — the official letter naming the delegation, and its back-and-forth
/// with the host.
///
/// The host replies by email, outside this system, so acknowledge and
/// request-changes are recorded by Protocol rather than detected.
/// </summary>
[Route("api/v1/nomination-letter")]
[Authorize]
[ApiVersion("1.0")]
public class NominationLetterController(INominationLetterService _letters, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    /// <summary>Current state, versions and log. Returns a not_generated shell
    /// when the mission has no letter yet.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.NominationLetter)]
    public async Task<IActionResult> Get([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _letters.GetAsync(eventId, ct));

    /// <summary>One version's pinned roster — who the host was actually sent.</summary>
    [HttpGet("versions/{versionId:guid}")]
    [HasPermission(PermissionCodes.NominationLetter)]
    public async Task<IActionResult> GetVersion(Guid versionId, CancellationToken ct)
        => ToResponse(await _letters.GetVersionAsync(versionId, ct));

    /// <summary>Issues a new version from the current roster.</summary>
    [HttpPost("generate")]
    [HasPermission(PermissionCodes.NominationLetter, AccessLevel.Write)]
    public async Task<IActionResult> Generate([FromBody] GenerateLetterRequest request, CancellationToken ct)
        => ToResponse(await _letters.GenerateAsync(request, _currentUser.UserId, ct));

    /// <summary>Records that the letter went to the host. Re-sending is allowed.</summary>
    [HttpPost("send")]
    [HasPermission(PermissionCodes.NominationLetter, AccessLevel.Write)]
    public async Task<IActionResult> Send([FromBody] SendLetterRequest request, CancellationToken ct)
        => ToResponse(await _letters.SendAsync(request, _currentUser.UserId, ct));

    [HttpPost("acknowledge")]
    [HasPermission(PermissionCodes.NominationLetter, AccessLevel.Write)]
    public async Task<IActionResult> Acknowledge([FromBody] LetterResponseRequest request, CancellationToken ct)
        => ToResponse(await _letters.AcknowledgeAsync(request, _currentUser.UserId, ct));

    /// <summary>Host asked for changes. The note is required.</summary>
    [HttpPost("request-changes")]
    [HasPermission(PermissionCodes.NominationLetter, AccessLevel.Write)]
    public async Task<IActionResult> RequestChanges([FromBody] LetterResponseRequest request, CancellationToken ct)
        => ToResponse(await _letters.RequestChangesAsync(request, _currentUser.UserId, ct));
}
