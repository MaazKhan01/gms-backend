using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.SupportChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers.v1;

// ============================================================================
// Direct driver <-> guest chat. Separate from SupportChatController, which is
// only the guest <-> admin support inbox — a DriverGuest thread never appears
// there, and vice versa.
//
// Both sides resolve through ICurrentUser: a driver token is a normal staff
// User token, and a guest token's NameIdentifier is the guest's linked User.Id
// (see VipAppService.BuildAccessToken), so one code path serves both. The
// service rejects any caller whose role isn't "driver" or "guest", and the
// thread endpoint rejects anyone who isn't one of the two participants.
// ============================================================================
[Route("api/v1/chat")]
[Authorize]
[ApiVersion("1.0")]
public class ChatController(ISupportChatService _supportChat, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Start-or-continue: target the counterpart with recipientUserId (either
    // direction) or recipientRole="driver" (guest sender only). Moved here from
    // api/v1/support-chat/driver-guest/messages.
    [HttpPost("messages"), EnableRateLimiting("chat")]
    public async Task<IActionResult> SendMessage([FromBody] SendDriverGuestMessageRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.SendDriverGuestMessageAsync(_currentUser.UserId, _currentUser.RoleName, request, ct));

    // The full thread by conversationId (the conversation's PublicId, as returned
    // on every sent message and in the new-message notification payload). Each
    // message carries isMine — sent vs received from the caller's own side.
    [HttpGet("threads/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetThread(Guid conversationId, [FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.GetDriverGuestThreadAsync(_currentUser.UserId, conversationId, request, ct));
}
