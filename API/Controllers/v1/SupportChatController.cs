using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.SupportChat;
using Core.ViewModel.VipApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers.v1;

// ============================================================================
// Admin/staff chat management — the "who has unread messages, reply, mark read"
// surface. Guest-side endpoints live on VipAppController (api/v1/vip-app/support/*).
// conversationId is always the conversation's PublicId, never the internal int.
// ============================================================================
[Route("api/v1/support-chat")]
[Authorize]
[ApiVersion("1.0")]
public class SupportChatController(ISupportChatService _supportChat) : Controllers.BaseApiController
{
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
