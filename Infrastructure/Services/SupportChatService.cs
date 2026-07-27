using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Common;
using Core.Common.Interfaces;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;
using Core.ViewModel.SupportChat;
using Core.ViewModel.VipApp;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

// ============================================================================
// Support Chat — guest ↔ admin. A guest has exactly one implicit conversation
// (SupportConversation, one row per GuestId), matching SupportMessage's existing
// one-thread-per-guest design. Both guest and admin entry points funnel through
// the same get-or-create + append + notify sequence below.
// ============================================================================
public class SupportChatService(
    IUnitOfWork _unitOfWork,
    ICurrentUser _currentUser,
    INotificationManagerService _notificationManagerService,
    ILogger<SupportChatService> _logger) : ISupportChatService
{
    private const int MaxBodyLength = 4000;
    private const int PreviewLength = 200;

    // ============================================================
    // Guest-facing
    // ============================================================
    public async Task<ApiResponse<List<SupportConversationSummaryResponse>>> GetMyConversationsAsync(int guestId, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations.QueryNoTracking()
            .Include(c => c.Guest)
            .FirstOrDefaultAsync(c => c.GuestId == guestId, ct);

        var data = new List<SupportConversationSummaryResponse>();
        if (conversation != null)
            data.Add(MapSummary(conversation, conversation.UnreadByGuestCount));

        return ApiResponse<List<SupportConversationSummaryResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<PaginatedResponse<SupportMessageResponse>>> GetGuestMessagesAsync(int guestId, PagedRequest request, CancellationToken ct = default)
    {
        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 50;

        var query = _unitOfWork.SupportMessages.QueryNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.GuestId == guestId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        items.Reverse(); // oldest-first within the returned window, like a chat feed

        var data = items.Select(m => MapMessage(m)).ToList();
        return ApiResponse<PaginatedResponse<SupportMessageResponse>>.SuccessResponse(
            new PaginatedResponse<SupportMessageResponse>(data, total, page, size));
    }

    public async Task<ApiResponse<SupportMessageResponse>> SendGuestMessageAsync(int guestId, SendSupportMessageRequest request, CancellationToken ct = default)
    {
        var validation = ValidateBody(request?.Body);
        if (validation != null)
            return ApiResponse<SupportMessageResponse>.ErrorResponse(validation);

        var conversation = await GetOrCreateConversationAsync(guestId, ct);
        if (conversation.Status == SupportChatStatuses.Closed)
        {
            conversation.Status = SupportChatStatuses.Open;
            conversation.ClosedAt = null;
            conversation.ClosedByUserId = null;
        }

        var body = request.Body.Trim();
        var msg = new SupportMessage
        {
            GuestId = guestId,
            ConversationId = conversation.Id,
            Body = body,
            FromGuest = true,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        await _unitOfWork.SupportMessages.AddAsync(msg, ct);

        conversation.LastMessageAt = msg.SentAt;
        conversation.LastMessagePreview = Truncate(body, PreviewLength);
        conversation.LastMessageFromGuest = true;
        conversation.UnreadByAdminCount += 1;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);

        await NotifyAdminsAsync(conversation, ct);

        return ApiResponse<SupportMessageResponse>.SuccessResponse(MapMessage(msg), "Message sent");
    }

    public async Task<ApiResponse<bool>> MarkReadByGuestAsync(int guestId, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations.FindFirstOrDefaultAsync(c => c.GuestId == guestId, ct);
        if (conversation is null)
            return ApiResponse<bool>.SuccessResponse(true, "Nothing to mark read");

        var unread = (await _unitOfWork.SupportMessages
            .FindAsync(m => m.ConversationId == conversation.Id && !m.FromGuest && !m.IsRead, ct)).ToList();

        var now = DateTime.UtcNow;
        foreach (var m in unread) { m.IsRead = true; m.ReadAt = now; }
        if (unread.Count > 0) _unitOfWork.SupportMessages.UpdateRange(unread);

        conversation.UnreadByGuestCount = 0;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, $"{unread.Count} marked read");
    }

    // ============================================================
    // Admin-facing
    // ============================================================
    public async Task<ApiResponse<PaginatedResponse<SupportConversationSummaryResponse>>> GetConversationsAsync(SupportConversationPagedRequest request, CancellationToken ct = default)
    {
        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 20;

        var query = _unitOfWork.SupportConversations.QueryNoTracking().Include(c => c.Guest).AsQueryable();

        if (request?.OnlyUnread == true)
            query = query.Where(c => c.UnreadByAdminCount > 0);

        if (!string.IsNullOrWhiteSpace(request?.Status))
            query = query.Where(c => c.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
            query = query.Where(c =>
                (c.Guest.FirstName + " " + c.Guest.LastName).Contains(request.SearchTerm) ||
                (c.Guest.Email != null && c.Guest.Email.Contains(request.SearchTerm)));

        var total = await query.CountAsync(ct);

        // Unread-first, then most recently active — "only users with unread
        // messages should appear prominently" without hiding the rest.
        var items = await query
            .OrderByDescending(c => c.UnreadByAdminCount > 0)
            .ThenByDescending(c => c.LastMessageAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        var data = items.Select(c => MapSummary(c, c.UnreadByAdminCount)).ToList();
        return ApiResponse<PaginatedResponse<SupportConversationSummaryResponse>>.SuccessResponse(
            new PaginatedResponse<SupportConversationSummaryResponse>(data, total, page, size));
    }

    public async Task<ApiResponse<PaginatedResponse<SupportMessageResponse>>> GetMessagesAsync(Guid conversationId, PagedRequest request, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations.FindFirstOrDefaultAsync(c => c.PublicId == conversationId, ct);
        if (conversation is null)
            return ApiResponse<PaginatedResponse<SupportMessageResponse>>.NotFoundResponse("Conversation not found");

        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 50;

        var query = _unitOfWork.SupportMessages.QueryNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.ConversationId == conversation.Id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        items.Reverse();

        var data = items.Select(m => MapMessage(m)).ToList();
        return ApiResponse<PaginatedResponse<SupportMessageResponse>>.SuccessResponse(
            new PaginatedResponse<SupportMessageResponse>(data, total, page, size));
    }

    public async Task<ApiResponse<SupportMessageResponse>> ReplyAsync(Guid conversationId, SendSupportMessageRequest request, CancellationToken ct = default)
    {
        var validation = ValidateBody(request?.Body);
        if (validation != null)
            return ApiResponse<SupportMessageResponse>.ErrorResponse(validation);

        var conversation = await _unitOfWork.SupportConversations.FindFirstOrDefaultAsync(c => c.PublicId == conversationId, ct);
        if (conversation is null)
            return ApiResponse<SupportMessageResponse>.NotFoundResponse("Conversation not found");

        var body = request.Body.Trim();
        var adminUserId = _currentUser.UserId;
        var msg = new SupportMessage
        {
            GuestId = conversation.GuestId,
            ConversationId = conversation.Id,
            Body = body,
            FromGuest = false,
            SenderUserId = adminUserId,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        await _unitOfWork.SupportMessages.AddAsync(msg, ct);

        conversation.LastMessageAt = msg.SentAt;
        conversation.LastMessagePreview = Truncate(body, PreviewLength);
        conversation.LastMessageFromGuest = false;
        conversation.UnreadByGuestCount += 1;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);

        await NotifyGuestAsync(conversation, ct);

        var senderName = _currentUser.UserInfo != null
            ? $"{_currentUser.UserInfo.FirstName} {_currentUser.UserInfo.LastName}".Trim()
            : null;
        return ApiResponse<SupportMessageResponse>.SuccessResponse(MapMessage(msg, senderName), "Reply sent");
    }

    public async Task<ApiResponse<bool>> MarkReadByAdminAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations.FindFirstOrDefaultAsync(c => c.PublicId == conversationId, ct);
        if (conversation is null)
            return ApiResponse<bool>.NotFoundResponse("Conversation not found");

        var unread = (await _unitOfWork.SupportMessages
            .FindAsync(m => m.ConversationId == conversation.Id && m.FromGuest && !m.IsRead, ct)).ToList();

        var now = DateTime.UtcNow;
        foreach (var m in unread) { m.IsRead = true; m.ReadAt = now; }
        if (unread.Count > 0) _unitOfWork.SupportMessages.UpdateRange(unread);

        conversation.UnreadByAdminCount = 0;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, $"{unread.Count} marked read");
    }

    public async Task<ApiResponse<bool>> CloseAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations.FindFirstOrDefaultAsync(c => c.PublicId == conversationId, ct);
        if (conversation is null)
            return ApiResponse<bool>.NotFoundResponse("Conversation not found");

        conversation.Status = SupportChatStatuses.Closed;
        conversation.ClosedAt = DateTime.UtcNow;
        conversation.ClosedByUserId = _currentUser.UserId;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, "Conversation closed");
    }

    public async Task<ApiResponse<bool>> ReopenAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations.FindFirstOrDefaultAsync(c => c.PublicId == conversationId, ct);
        if (conversation is null)
            return ApiResponse<bool>.NotFoundResponse("Conversation not found");

        conversation.Status = SupportChatStatuses.Open;
        conversation.ClosedAt = null;
        conversation.ClosedByUserId = null;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, "Conversation reopened");
    }

    // ============================================================
    // Helpers
    // ============================================================
    private async Task<SupportConversation> GetOrCreateConversationAsync(int guestId, CancellationToken ct)
    {
        var conversation = await _unitOfWork.SupportConversations.FindFirstOrDefaultAsync(c => c.GuestId == guestId, ct);
        if (conversation != null) return conversation;

        conversation = new SupportConversation
        {
            GuestId = guestId,
            Status = SupportChatStatuses.Open,
        };
        await _unitOfWork.SupportConversations.AddAsync(conversation, ct);
        await _unitOfWork.SaveChangesAsync(ct); // need the generated Id for SupportMessage.ConversationId
        return conversation;
    }

    // Every User holding SupportChatManage is notified — not a hardcoded single
    // admin id, so granting the permission to a second User is the entire
    // "add another admin" story. Persistence + realtime push both happen inside
    // INotificationManagerService.SendToPermissionAsync — this is just the content.
    private async Task NotifyAdminsAsync(SupportConversation conversation, CancellationToken ct)
    {
        try
        {
            await _notificationManagerService.SendToPermissionAsync(PermissionCodes.SupportChatManage, new NotificationContent
            {
                Title = "New support message",
                Message = conversation.LastMessagePreview,
                Type = "support_message",
                RedirectUrl = $"/admin/support-chat/{conversation.PublicId}",
                Data = new Dictionary<string, string> { ["conversationId"] = conversation.PublicId.ToString() },
                Topic = RealtimeTopics.SupportMessageNew // preserve the existing wire event name the admin UI listens on
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying admins of new support message");
        }
    }

    private async Task NotifyGuestAsync(SupportConversation conversation, CancellationToken ct)
    {
        try
        {
            await _notificationManagerService.SendToGuestAsync(conversation.GuestId, new NotificationContent
            {
                Title = "New reply from support",
                Message = conversation.LastMessagePreview,
                Type = "support_reply",
                RedirectUrl = $"/support/{conversation.PublicId}",
                Data = new Dictionary<string, string> { ["conversationId"] = conversation.PublicId.ToString() },
                Topic = RealtimeTopics.SupportMessageNew // preserve the existing wire event name the guest app listens on
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying guest of support reply");
        }
    }

    private static string ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Message body is required";
        if (body.Trim().Length > MaxBodyLength)
            return $"Message cannot exceed {MaxBodyLength} characters";
        return null;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

    private static SupportConversationSummaryResponse MapSummary(SupportConversation c, int unreadCount) => new()
    {
        Id = c.PublicId,
        GuestId = c.Guest.PublicId,
        GuestName = $"{c.Guest.FirstName} {c.Guest.LastName}".Trim(),
        GuestEmail = c.Guest.Email,
        Status = c.Status,
        LastMessagePreview = c.LastMessagePreview,
        LastMessageAt = c.LastMessageAt,
        LastMessageFromGuest = c.LastMessageFromGuest,
        UnreadCount = unreadCount
    };

    private static SupportMessageResponse MapMessage(SupportMessage m, string senderName = null) => new()
    {
        Id = m.PublicId,
        Body = m.Body,
        FromGuest = m.FromGuest,
        SentAt = m.SentAt,
        IsRead = m.IsRead,
        ReadAt = m.ReadAt,
        AttachmentUrl = m.AttachmentUrl,
        AttachmentType = m.AttachmentType,
        SenderName = senderName ?? (m.SenderUser != null ? $"{m.SenderUser.FirstName} {m.SenderUser.LastName}".Trim() : null)
    };
}
