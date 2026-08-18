using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.SupportChat;
using Core.ViewModel.VipApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers.v1;

// ============================================================================
// Support chat — both sides of the guest ↔ admin conversation live here:
//   - api/v1/support-chat/my/*            guest's own conversation (ICurrentGuest)
//   - api/v1/support-chat/conversations/*  admin inbox across all guests (ICurrentUser + permissions)
// conversationId is always the conversation's PublicId, never the internal int.
// Driver ↔ guest chat is a different conversation type and lives in
// ChatController (api/v1/chat) — every query here is AdminSupport-only.
// ============================================================================
[Route("api/v1/support-chat")]
[Authorize]
[ApiVersion("1.0")]
public class SupportChatController(ISupportChatService _supportChat, ICurrentGuest _currentGuest) : Controllers.BaseApiController
{
    private int GuestId => _currentGuest.GuestId;

    // ================= Guest side — "my" own conversation =================
    [HttpGet("my/conversations")]
    public async Task<IActionResult> GetMyConversations(CancellationToken ct)
        => ToResponse(await _supportChat.GetMyConversationsAsync(GuestId, ct));

    [HttpGet("my/messages")]
    public async Task<IActionResult> GetMyMessages([FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.GetGuestMessagesAsync(GuestId, request, ct));

    [HttpPost("my/messages"), EnableRateLimiting("chat")]
    public async Task<IActionResult> SendMyMessage([FromBody] SendSupportMessageRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.SendGuestMessageAsync(GuestId, request, ct));

    [HttpPost("my/messages/read")]
    public async Task<IActionResult> MarkMyMessagesRead(CancellationToken ct)
        => ToResponse(await _supportChat.MarkReadByGuestAsync(GuestId, ct));

    // ================= Admin side — inbox across all guests =================
    [HttpGet("conversations")]
    [HasPermission(PermissionCodes.SupportChatView)]
    public async Task<IActionResult> GetConversations([FromQuery] SupportConversationPagedRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.GetConversationsAsync(request, ct));

    [HttpGet("conversations/{conversationId:guid}/messages")]
    [HasPermission(PermissionCodes.SupportChatView)]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] PagedRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.GetMessagesAsync(conversationId, request, ct));

    [HttpPost("conversations/{conversationId:guid}/messages"), EnableRateLimiting("chat")]
    [HasPermission(PermissionCodes.SupportChatManage)]
    public async Task<IActionResult> Reply(Guid conversationId, [FromBody] SendSupportMessageRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.ReplyAsync(conversationId, request, ct));

    // Admin starts (or continues) a conversation by guest id — no prior
    // conversation needs to exist. The literal "by-guest" segment keeps this
    // from colliding with the {conversationId:guid} route above.
    // Person-level: one support thread per human, not per event. The path id is
    // a Guest.PublicId (GuestResponse.personId).
    [HttpPost("conversations/by-guest/{personId:guid}/messages"), EnableRateLimiting("chat")]
    [HasPermission(PermissionCodes.SupportChatManage)]
    public async Task<IActionResult> StartOrReply(Guid personId, [FromBody] SendSupportMessageRequest request, CancellationToken ct)
        => ToResponse(await _supportChat.StartOrReplyByGuestAsync(personId, request, ct));

    [HttpPost("conversations/{conversationId:guid}/read")]
    [HasPermission(PermissionCodes.SupportChatManage)]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken ct)
        => ToResponse(await _supportChat.MarkReadByAdminAsync(conversationId, ct));

    [HttpPost("conversations/{conversationId:guid}/close")]
    [HasPermission(PermissionCodes.SupportChatManage)]
    public async Task<IActionResult> Close(Guid conversationId, CancellationToken ct)
        => ToResponse(await _supportChat.CloseAsync(conversationId, ct));

    [HttpPost("conversations/{conversationId:guid}/reopen")]
    [HasPermission(PermissionCodes.SupportChatManage)]
    public async Task<IActionResult> Reopen(Guid conversationId, CancellationToken ct)
        => ToResponse(await _supportChat.ReopenAsync(conversationId, ct));
}
