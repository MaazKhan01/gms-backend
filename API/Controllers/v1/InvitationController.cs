using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Services;
using Core.ViewModel.Invitation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{
    // PUBLIC (no-login) surface for guests to view + respond to their invitation
    // via the tokenised link in the invitation email. No class-level [Authorize].
    [Route("api/v1/[controller]")]
    [ApiVersion("1.0")]
    public class InvitationController(IInvitationService _invitationService) : Controllers.BaseApiController
    {
        [HttpGet("{token:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInvitation(Guid token, CancellationToken ct)
        {
            var result = await _invitationService.GetByTokenAsync(token, ct);
            return ToResponse(result);
        }

        [HttpPost("{token:guid}/respond")]
        [AllowAnonymous]
        public async Task<IActionResult> Respond(Guid token, [FromBody] RespondToInvitationRequest request, CancellationToken ct)
        {
            var result = await _invitationService.RespondAsync(token, request, ct);
            return ToResponse(result);
        }
    }
}
