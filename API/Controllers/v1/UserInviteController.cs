using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Services;
using Core.ViewModel.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{
    // PUBLIC (no-login) surface for an invited user to view + accept their
    // invite via the tokenised link in the invite email. No class-level [Authorize].
    [Route("api/v1/user-invite")]
    [ApiVersion("1.0")]
    public class UserInviteController(IUserService _userService) : Controllers.BaseApiController
    {
        [HttpGet("{token:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInvite(Guid token, CancellationToken ct)
        {
            var result = await _userService.GetInviteByTokenAsync(token, ct);
            return ToResponse(result);
        }

        [HttpPost("{token:guid}/accept")]
        [AllowAnonymous]
        public async Task<IActionResult> Accept(Guid token, [FromBody] AcceptInviteRequest request, CancellationToken ct)
        {
            var result = await _userService.AcceptInviteAsync(token, request, ct);
            return ToResponse(result);
        }
    }
}
