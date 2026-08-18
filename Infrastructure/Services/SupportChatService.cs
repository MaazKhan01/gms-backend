using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Common;
using Core.Common.Interfaces;
using Core.Constants;
using Core.Constants.Notification;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;
using Core.ViewModel.SupportChat;
using Core.ViewModel.VipApp;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

// ============================================================================
// Support Chat — two products share the same SupportConversation/SupportMessage
// tables (see SupportConversation.Type):
//   AdminSupport — guest <-> admin. One implicit conversation per guest.
//   DriverGuest  — driver <-> guest. One conversation per (guest, driver) pair.
// Every guest/admin/driver identity in this file is a Users.Id — Guest is now
// a 1:1 profile extension of User (Guest.UserId), so a guest's "own" id for
// chat purposes is Guest.UserId, never Guest.Id. Guest.Id (and its PublicId)
// is still what the guest-facing DTOs expose as "GuestId", to keep the wire
// contract unchanged for existing admin/guest clients.
// ============================================================================
public class SupportChatService(
    IUnitOfWork _unitOfWork,
    ICurrentUser _currentUser,
    INotificationManagerService _notificationManagerService,
    ILogger<SupportChatService> _logger) : ISupportChatService
{
    private const int MaxBodyLength = 4000;

    // Body chars carried inline in a push payload. FCM's limit is ~4KB for the
    // whole data dictionary; 1000 leaves headroom for the other keys and covers
    // essentially every real chat message.
    private const int MaxPushBodyLength = 1000;
    private const int PreviewLength = 200;

    // ============================================================
    // Guest-facing (AdminSupport only — a DriverGuest thread never shows up
    // in "my conversation with support")
    // ============================================================
    public async Task<ApiResponse<List<SupportConversationSummaryResponse>>> GetMyConversationsAsync(int guestId, CancellationToken ct = default)
    {
        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Id == guestId, ct);
        if (guest is null)
            return ApiResponse<List<SupportConversationSummaryResponse>>.SuccessResponse(new());

        var conversation = await _unitOfWork.SupportConversations.QueryNoTracking()
            .Include(c => c.User).ThenInclude(u => u.GuestProfile).ThenInclude(g => g.Nationality)
            // Tier/organisation live on the participation now; the inbox shows the
            // person's most recent one (ordered in MapSummary).
            .Include(c => c.User).ThenInclude(u => u.GuestProfile).ThenInclude(g => g.EventGuests).ThenInclude(eg => eg.OrganizationRef)
            .FirstOrDefaultAsync(c => c.UserId == guest.UserId && c.Type == SupportChatTypes.AdminSupport, ct);

        var data = new List<SupportConversationSummaryResponse>();
        if (conversation != null)
            data.Add(MapSummary(conversation, conversation.UnreadByGuestCount));

        return ApiResponse<List<SupportConversationSummaryResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<PaginatedResponse<SupportMessageResponse>>> GetGuestMessagesAsync(int guestId, PagedRequest request, CancellationToken ct = default)
    {
        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 50;

        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Id == guestId, ct);
        if (guest is null)
            return ApiResponse<PaginatedResponse<SupportMessageResponse>>.SuccessResponse(new(new(), 0, page, size));

        var query = _unitOfWork.SupportMessages.QueryNoTracking()
            .Include(m => m.SenderUser)
            .Include(m => m.Conversation)
            .Where(m => m.UserId == guest.UserId && m.Conversation.Type == SupportChatTypes.AdminSupport);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        items.Reverse(); // oldest-first within the returned window, like a chat feed

        // The caller is the guest, so their own messages are the FromGuest ones.
        var data = items.Select(m => MapMessage(m, isMine: m.FromGuest)).ToList();
        return ApiResponse<PaginatedResponse<SupportMessageResponse>>.SuccessResponse(
            new PaginatedResponse<SupportMessageResponse>(data, total, page, size));
    }

    public async Task<ApiResponse<SupportMessageResponse>> SendGuestMessageAsync(int guestId, SendSupportMessageRequest request, CancellationToken ct = default)
    {
        var validation = ValidateSend(request);
        if (validation != null)
            return ApiResponse<SupportMessageResponse>.ErrorResponse(validation);

        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Id == guestId, ct);
        if (guest is null)
            return ApiResponse<SupportMessageResponse>.NotFoundResponse("Guest not found");

        var conversation = await GetOrCreateConversationAsync(guest.UserId, SupportChatTypes.AdminSupport, null, ct);
        if (conversation.Status == SupportChatStatuses.Closed)
        {
            conversation.Status = SupportChatStatuses.Open;
            conversation.ClosedAt = null;
            conversation.ClosedByUserId = null;
        }

        var body = request.Body?.Trim() ?? string.Empty;
        var msg = new SupportMessage
        {
            UserId = guest.UserId,
            SenderUserId = guest.UserId,
            ConversationId = conversation.Id,
            Conversation = conversation,
            Body = body,
            FromGuest = true,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            AttachmentUrl = request.AttachmentUrl,
            AttachmentType = request.AttachmentType,
        };
        await _unitOfWork.SupportMessages.AddAsync(msg, ct);

        conversation.LastMessageAt = msg.SentAt;
        conversation.LastMessagePreview = ComputePreview(body, request.AttachmentType);
        conversation.LastMessageFromGuest = true;
        conversation.UnreadByAdminCount += 1;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);

        // No senderName: the admin inbox shows the guest's own thread, so the
        // sender is never ambiguous there.
        await NotifyAdminsAsync(conversation, msg, ct);

        return ApiResponse<SupportMessageResponse>.SuccessResponse(MapMessage(msg, isMine: true), "Message sent");
    }

    public async Task<ApiResponse<bool>> MarkReadByGuestAsync(int guestId, CancellationToken ct = default)
    {
        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Id == guestId, ct);
        if (guest is null)
            return ApiResponse<bool>.SuccessResponse(true, "Nothing to mark read");

        var conversation = await _unitOfWork.SupportConversations
            .FindFirstOrDefaultAsync(c => c.UserId == guest.UserId && c.Type == SupportChatTypes.AdminSupport, ct);
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
    // Admin-facing (AdminSupport only)
    // ============================================================
    public async Task<ApiResponse<PaginatedResponse<SupportConversationSummaryResponse>>> GetConversationsAsync(SupportConversationPagedRequest request, CancellationToken ct = default)
    {
        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 20;

        var query = _unitOfWork.SupportConversations.QueryNoTracking()
            .Include(c => c.User).ThenInclude(u => u.GuestProfile).ThenInclude(g => g.Nationality)
            // Tier/organisation live on the participation now; the inbox shows the
            // person's most recent one (ordered in MapSummary).
            .Include(c => c.User).ThenInclude(u => u.GuestProfile).ThenInclude(g => g.EventGuests).ThenInclude(eg => eg.OrganizationRef)
            .Where(c => c.Type == SupportChatTypes.AdminSupport);

        if (request?.OnlyUnread == true)
            query = query.Where(c => c.UnreadByAdminCount > 0);

        if (!string.IsNullOrWhiteSpace(request?.Status))
            query = query.Where(c => c.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
            query = query.Where(c =>
                (c.User.FirstName + " " + c.User.LastName).Contains(request.SearchTerm) ||
                (c.User.GuestProfile.Email != null && c.User.GuestProfile.Email.Contains(request.SearchTerm)));

        if (!string.IsNullOrWhiteSpace(request?.Tier))
        {
            var tier = request.Tier.ToLower();
            query = query.Where(c => c.User.GuestProfile.EventGuests.Any(eg => eg.Tier != null && eg.Tier.ToLower() == tier));
        }

        if (request?.OrganizationId.HasValue == true && request.OrganizationId != Guid.Empty)
        {
            var org = await _unitOfWork.Organizations.GetByPublicIdAsync(request.OrganizationId.Value, ct);
            query = query.Where(c => org != null && c.User.GuestProfile.EventGuests.Any(eg => eg.OrganizationId == org.Id));
        }

        if (request?.NationalityId.HasValue == true && request.NationalityId != Guid.Empty)
        {
            var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(request.NationalityId.Value, ct);
            query = query.Where(c => nat != null && c.User.GuestProfile.NationalityId == nat.Id);
        }

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
        var conversation = await _unitOfWork.SupportConversations
            .FindFirstOrDefaultAsync(c => c.PublicId == conversationId && c.Type == SupportChatTypes.AdminSupport, ct);
        if (conversation is null)
            return ApiResponse<PaginatedResponse<SupportMessageResponse>>.NotFoundResponse("Conversation not found");

        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 50;

        var query = _unitOfWork.SupportMessages.QueryNoTracking()
            .Include(m => m.SenderUser)
            .Include(m => m.Conversation)
            .Where(m => m.ConversationId == conversation.Id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        items.Reverse();

        // The caller is an admin, so anything not from the guest is theirs.
        var data = items.Select(m => MapMessage(m, isMine: !m.FromGuest)).ToList();
        return ApiResponse<PaginatedResponse<SupportMessageResponse>>.SuccessResponse(
            new PaginatedResponse<SupportMessageResponse>(data, total, page, size));
    }

    public async Task<ApiResponse<SupportMessageResponse>> ReplyAsync(Guid conversationId, SendSupportMessageRequest request, CancellationToken ct = default)
    {
        var validation = ValidateSend(request);
        if (validation != null)
            return ApiResponse<SupportMessageResponse>.ErrorResponse(validation);

        var conversation = await _unitOfWork.SupportConversations
            .FindFirstOrDefaultAsync(c => c.PublicId == conversationId && c.Type == SupportChatTypes.AdminSupport, ct);
        if (conversation is null)
            return ApiResponse<SupportMessageResponse>.NotFoundResponse("Conversation not found");

        return await SendAdminMessageAsync(conversation, request, ct);
    }

    // Admin-initiated: get-or-create the guest's AdminSupport conversation, then
    // send through the exact same path ReplyAsync uses. Safe to call even when a
    // conversation already exists for this guest (e.g. the admin's local list
    // was stale) — it just continues that thread rather than erroring or duplicating it.
    /// <summary>Admin starts/continues the thread with a PERSON —
    /// <paramref name="personId"/> is a Guest.PublicId, not a participation.</summary>
    public async Task<ApiResponse<SupportMessageResponse>> StartOrReplyByGuestAsync(Guid personId, SendSupportMessageRequest request, CancellationToken ct = default)
    {
        var validation = ValidateSend(request);
        if (validation != null)
            return ApiResponse<SupportMessageResponse>.ErrorResponse(validation);

        var guest = await _unitOfWork.Guests.GetByPublicIdAsync(personId, ct);
        if (guest is null)
            return ApiResponse<SupportMessageResponse>.NotFoundResponse("Guest not found");

        var conversation = await GetOrCreateConversationAsync(guest.UserId, SupportChatTypes.AdminSupport, null, ct);
        return await SendAdminMessageAsync(conversation, request, ct);
    }

    // Shared by ReplyAsync and StartOrReplyByGuestAsync. Reopens a Closed
    // conversation on send — the admin UI normally requires an explicit
    // "Reopen" click first, but StartOrReplyByGuestAsync can land here without
    // that check ever running (a guest picked from the "start new chat" list
    // may already have an existing, closed conversation the admin's client
    // didn't know about) — so this is the actual safety net, not the UI gate.
    private async Task<ApiResponse<SupportMessageResponse>> SendAdminMessageAsync(
        SupportConversation conversation, SendSupportMessageRequest request, CancellationToken ct)
    {
        if (conversation.Status == SupportChatStatuses.Closed)
        {
            conversation.Status = SupportChatStatuses.Open;
            conversation.ClosedAt = null;
            conversation.ClosedByUserId = null;
        }

        var body = request.Body?.Trim() ?? string.Empty;
        var adminUserId = _currentUser.UserId;
        var msg = new SupportMessage
        {
            UserId = conversation.UserId,
            ConversationId = conversation.Id,
            Conversation = conversation,
            Body = body,
            FromGuest = false,
            SenderUserId = adminUserId,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            AttachmentUrl = request.AttachmentUrl,
            AttachmentType = request.AttachmentType,
        };
        await _unitOfWork.SupportMessages.AddAsync(msg, ct);

        conversation.LastMessageAt = msg.SentAt;
        conversation.LastMessagePreview = ComputePreview(body, request.AttachmentType);
        conversation.LastMessageFromGuest = false;
        conversation.UnreadByGuestCount += 1;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);

        var senderName = _currentUser.UserInfo != null
            ? $"{_currentUser.UserInfo.FirstName} {_currentUser.UserInfo.LastName}".Trim()
            : null;

        await NotifyGuestAsync(conversation, msg, senderName, ct);

        return ApiResponse<SupportMessageResponse>.SuccessResponse(MapMessage(msg, isMine: true, senderName), "Message sent");
    }

    public async Task<ApiResponse<bool>> MarkReadByAdminAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations
            .FindFirstOrDefaultAsync(c => c.PublicId == conversationId && c.Type == SupportChatTypes.AdminSupport, ct);
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
        var conversation = await _unitOfWork.SupportConversations
            .FindFirstOrDefaultAsync(c => c.PublicId == conversationId && c.Type == SupportChatTypes.AdminSupport, ct);
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
        var conversation = await _unitOfWork.SupportConversations
            .FindFirstOrDefaultAsync(c => c.PublicId == conversationId && c.Type == SupportChatTypes.AdminSupport, ct);
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
    // Driver <-> Guest (task: one endpoint, start-or-reuse + targeting + validation)
    // ============================================================
    public async Task<ApiResponse<SupportMessageResponse>> SendDriverGuestMessageAsync(
        int senderUserId, string senderRole, SendDriverGuestMessageRequest request, CancellationToken ct = default)
    {
        var validation = ValidateSend(new SendSupportMessageRequest
        {
            Body = request?.Body,
            AttachmentUrl = request?.AttachmentUrl,
            AttachmentType = request?.AttachmentType
        });
        if (validation != null)
            return ApiResponse<SupportMessageResponse>.ErrorResponse(validation);

        var isDriverSender = string.Equals(senderRole, Roles.DRIVER, StringComparison.OrdinalIgnoreCase);
        var isGuestSender = string.Equals(senderRole, Roles.GUEST, StringComparison.OrdinalIgnoreCase);
        if (!isDriverSender && !isGuestSender)
            return ApiResponse<SupportMessageResponse>.ForbiddenResponse("Only drivers and guests can use this endpoint");

        int guestUserId, driverUserId;

        if (request.RecipientUserId.HasValue)
        {
            var recipient = await _unitOfWork.Users.Query()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PublicId == request.RecipientUserId.Value, ct);

            if (recipient is null)
                return ApiResponse<SupportMessageResponse>.NotFoundResponse("Recipient not found");
            if (!recipient.IsActive)
                return ApiResponse<SupportMessageResponse>.ErrorResponse("Recipient is inactive");

            var recipientRole = recipient.Role?.Code;
            if (isGuestSender)
            {
                if (!string.Equals(recipientRole, Roles.DRIVER, StringComparison.OrdinalIgnoreCase))
                    return ApiResponse<SupportMessageResponse>.ErrorResponse("Invalid recipient — expected a driver");
                driverUserId = recipient.Id;
                guestUserId = senderUserId;
            }
            else
            {
                if (!string.Equals(recipientRole, Roles.GUEST, StringComparison.OrdinalIgnoreCase))
                    return ApiResponse<SupportMessageResponse>.ErrorResponse("Invalid recipient — expected a guest");
                guestUserId = recipient.Id;
                driverUserId = senderUserId;
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.RecipientRole))
        {
            // Role-based targeting is only meaningful for a guest reaching "a"
            // driver — resolved from the trips they've been driven on. A driver has
            // (potentially) many guests, so there's no equivalent implicit target.
            if (!isGuestSender || !string.Equals(request.RecipientRole, Roles.DRIVER, StringComparison.OrdinalIgnoreCase))
                return ApiResponse<SupportMessageResponse>.ErrorResponse(
                    "recipientRole targeting is only supported for a guest messaging \"driver\" — a driver must specify recipientUserId");

            // Transport owns the pairing (DriverId + EventGuestId), so this spans
            // every event the person is on — chat is person-level and a driver on
            // any of their trips is someone they can legitimately message.
            var assignedDriverIds = await _unitOfWork.Transports.Query()
                .Where(t => t.DriverId != null && t.EventGuest.Guest.UserId == senderUserId)
                .Select(t => t.DriverId.Value)
                .Distinct()
                .ToListAsync(ct);

            if (assignedDriverIds.Count == 0)
                return ApiResponse<SupportMessageResponse>.NotFoundResponse("No driver is assigned to you yet");
            if (assignedDriverIds.Count > 1)
                return ApiResponse<SupportMessageResponse>.ErrorResponse(
                    "More than one driver is assigned — specify recipientUserId to pick one");

            var driverProfile = await _unitOfWork.DriverProfiles.FindFirstOrDefaultAsync(d => d.Id == assignedDriverIds[0], ct);
            if (driverProfile is null)
                return ApiResponse<SupportMessageResponse>.NotFoundResponse("Assigned driver not found");

            guestUserId = senderUserId;
            driverUserId = driverProfile.UserId;
        }
        else
        {
            return ApiResponse<SupportMessageResponse>.ErrorResponse("recipientUserId or recipientRole is required");
        }

        // Both targeting paths land here: the pair must share at least one trip.
        // Checked for explicit recipientUserId too — otherwise any driver token
        // could open a thread with any guest.
        if (!await AreDriverAndGuestLinkedAsync(driverUserId, guestUserId, ct))
            return ApiResponse<SupportMessageResponse>.ForbiddenResponse(
                "You can only message a guest you are assigned to drive");

        var conversation = await GetOrCreateConversationAsync(guestUserId, SupportChatTypes.DriverGuest, driverUserId, ct);
        if (conversation.Status == SupportChatStatuses.Closed)
        {
            conversation.Status = SupportChatStatuses.Open;
            conversation.ClosedAt = null;
            conversation.ClosedByUserId = null;
        }

        var fromGuest = senderUserId == guestUserId;
        var body = request.Body?.Trim() ?? string.Empty;
        var msg = new SupportMessage
        {
            UserId = guestUserId,
            SenderUserId = senderUserId,
            ConversationId = conversation.Id,
            Conversation = conversation,
            Body = body,
            FromGuest = fromGuest,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            AttachmentUrl = request.AttachmentUrl,
            AttachmentType = request.AttachmentType,
        };
        await _unitOfWork.SupportMessages.AddAsync(msg, ct);

        conversation.LastMessageAt = msg.SentAt;
        conversation.LastMessagePreview = ComputePreview(body, request.AttachmentType);
        conversation.LastMessageFromGuest = fromGuest;
        if (fromGuest) conversation.UnreadByAdminCount += 1; else conversation.UnreadByGuestCount += 1;
        _unitOfWork.SupportConversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(ct);

        var senderName = await _unitOfWork.Users.Query()
            .Where(u => u.Id == senderUserId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim())
            .FirstOrDefaultAsync(ct);

        // Notify after the name lookup, not before — the recipient renders the
        // bubble straight from the push payload, and a bubble needs its sender.
        var recipientUserId = fromGuest ? driverUserId : guestUserId;
        await NotifyDriverGuestRecipientAsync(recipientUserId, conversation, msg, senderName, ct);

        return ApiResponse<SupportMessageResponse>.SuccessResponse(MapMessage(msg, isMine: true, senderName), "Message sent");
    }

    /// <summary>Is this driver actually driving this guest? Transport is the only
    /// record of the pairing — DriverId plus EventGuestId — so a live (non-deleted)
    /// trip linking the two is what authorises the conversation. Person-level, not
    /// per-event: one trip on any event is enough.</summary>
    private async Task<bool> AreDriverAndGuestLinkedAsync(int driverUserId, int guestUserId, CancellationToken ct)
    {
        var driverProfileId = await _unitOfWork.DriverProfiles.Query()
            .Where(d => d.UserId == driverUserId)
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync(ct);
        if (driverProfileId is null) return false;

        return await _unitOfWork.Transports.Query()
            .AnyAsync(t => t.DriverId == driverProfileId
                && t.EventGuest.Guest.UserId == guestUserId, ct);
    }

    public async Task<ApiResponse<PaginatedResponse<SupportMessageResponse>>> GetDriverGuestThreadAsync(
        int callerUserId, Guid conversationId, PagedRequest request, CancellationToken ct = default)
    {
        var conversation = await _unitOfWork.SupportConversations.QueryNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == conversationId && c.Type == SupportChatTypes.DriverGuest, ct);
        if (conversation is null)
            return ApiResponse<PaginatedResponse<SupportMessageResponse>>.NotFoundResponse("Conversation not found");

        // Only the two participants — the guest (UserId) and the driver
        // (OtherUserId) — can read the thread. No admin path: DriverGuest threads
        // are deliberately absent from the admin inbox.
        if (callerUserId != conversation.UserId && callerUserId != conversation.OtherUserId)
            return ApiResponse<PaginatedResponse<SupportMessageResponse>>.ForbiddenResponse("You are not part of this conversation");

        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 50;

        var query = _unitOfWork.SupportMessages.QueryNoTracking()
            .Include(m => m.SenderUser)
            .Include(m => m.Conversation)
            .Where(m => m.ConversationId == conversation.Id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        items.Reverse(); // oldest-first within the returned window, like a chat feed

        // Both participants read this same thread, so "mine" is by actual sender.
        var data = items.Select(m => MapMessage(m, isMine: m.SenderUserId == callerUserId)).ToList();

        return ApiResponse<PaginatedResponse<SupportMessageResponse>>.SuccessResponse(
            new PaginatedResponse<SupportMessageResponse>(data, total, page, size));
    }

    // ============================================================
    // Helpers
    // ============================================================
    // otherUserId is null for AdminSupport (the "other side" is any admin, not
    // one fixed User) and the driver's User.Id for DriverGuest.
    private async Task<SupportConversation> GetOrCreateConversationAsync(int guestUserId, string type, int? otherUserId, CancellationToken ct)
    {
        var conversation = await _unitOfWork.SupportConversations
            .FindFirstOrDefaultAsync(c => c.UserId == guestUserId && c.Type == type && c.OtherUserId == otherUserId, ct);
        if (conversation != null) return conversation;

        conversation = new SupportConversation
        {
            UserId = guestUserId,
            Type = type,
            OtherUserId = otherUserId,
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
    private async Task NotifyAdminsAsync(SupportConversation conversation, SupportMessage msg, CancellationToken ct)
    {
        try
        {
            await _notificationManagerService.SendToPermissionAsync(PermissionCodes.SupportChatManage,
                NotificationTemplates.SupportMessageNew, ChatTokens(conversation, msg), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying admins of new support message");
        }
    }

    private async Task NotifyGuestAsync(SupportConversation conversation, SupportMessage msg, string senderName, CancellationToken ct)
    {
        try
        {
            await _notificationManagerService.SendToUserAsync(conversation.UserId,
                NotificationTemplates.SupportReplyNew, ChatTokens(conversation, msg, senderName), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying guest of support reply");
        }
    }

    private async Task NotifyDriverGuestRecipientAsync(
        int recipientUserId, SupportConversation conversation, SupportMessage msg, string senderName, CancellationToken ct)
    {
        try
        {
            await _notificationManagerService.SendToUserAsync(recipientUserId,
                NotificationTemplates.DriverGuestMessage, ChatTokens(conversation, msg, senderName), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying driver/guest chat recipient {UserId}", recipientUserId);
        }
    }

    // The apps have no socket, so FCM's string-only data dictionary is the only
    // thing a recipient with the chat screen open receives. Carrying the whole
    // message here lets it append the bubble straight from the push — no thread
    // fetch, no refresh. Anything omitted here forces an API round-trip.
    private static Dictionary<string, string> ChatTokens(
        SupportConversation conversation, SupportMessage msg = null, string senderName = null)
    {
        var tokens = new Dictionary<string, string>
        {
            ["conversationId"] = conversation.PublicId.ToString(),
            ["preview"] = conversation.LastMessagePreview,
        };
        if (msg is null) return tokens;

        // FCM caps the whole data payload at ~4KB while a body may be up to
        // MaxBodyLength (4000) — a long message would blow the limit and the
        // send would fail outright. Cap it and flag it: the client fetches the
        // thread only in that rare case, not on every message.
        var body = msg.Body ?? string.Empty;
        var truncated = body.Length > MaxPushBodyLength;

        tokens["messageId"] = msg.PublicId.ToString();
        tokens["senderUserId"] = msg.SenderUserId.ToString();
        tokens["fromGuest"] = msg.FromGuest ? "true" : "false";
        tokens["body"] = truncated ? Truncate(body, MaxPushBodyLength) : body;
        tokens["truncated"] = truncated ? "true" : "false";
        // Round-trip ("o") so the client can sort by this — FCM does not
        // guarantee delivery order, so arrival order is not message order.
        tokens["sentAt"] = msg.SentAt.ToString("o");
        if (senderName != null) tokens["senderName"] = senderName;
        if (msg.AttachmentUrl != null) tokens["attachmentUrl"] = msg.AttachmentUrl;
        if (msg.AttachmentType != null) tokens["attachmentType"] = msg.AttachmentType;
        return tokens;
    }

    // A message needs a body, an attachment, or both — never neither.
    private static string ValidateSend(SendSupportMessageRequest request)
    {
        var hasBody = !string.IsNullOrWhiteSpace(request?.Body);
        var hasAttachment = !string.IsNullOrWhiteSpace(request?.AttachmentUrl);
        if (!hasBody && !hasAttachment)
            return "Message body or an attachment is required";
        if (hasBody && request.Body.Trim().Length > MaxBodyLength)
            return $"Message cannot exceed {MaxBodyLength} characters";
        return null;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

    private static readonly Regex HtmlTag = new("<[^>]*>", RegexOptions.Compiled);

    // The admin composer sends rich-text HTML (Tiptap); the guest app always
    // sends plain text. Stripping tags here means the inbox's "latest message"
    // column is never asked to render raw markup as if it were plain text.
    private static string ComputePreview(string body, string attachmentType)
    {
        var plain = string.IsNullOrWhiteSpace(body) ? string.Empty : HtmlTag.Replace(body, string.Empty).Trim();
        if (plain.Length > 0) return Truncate(plain, PreviewLength);
        return attachmentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true
            ? "📷 Photo" : "📎 Attachment";
    }

    private static SupportConversationSummaryResponse MapSummary(SupportConversation c, int unreadCount)
    {
        var guestProfile = c.User.GuestProfile;
        // Tier/organisation are per event; the inbox is person-level, so show the
        // most recent participation's values rather than inventing a "current" one.
        var latest = guestProfile?.EventGuests?.OrderByDescending(eg => eg.CreatedAt).FirstOrDefault();
        return new()
        {
            Id = c.PublicId,
            GuestId = guestProfile?.PublicId ?? Guid.Empty,
            GuestName = $"{c.User.FirstName} {c.User.LastName}".Trim(),
            GuestEmail = guestProfile?.Email,
            GuestPhotoUrl = guestProfile?.PhotoUrl,
            Status = c.Status,
            LastMessagePreview = c.LastMessagePreview,
            LastMessageAt = c.LastMessageAt,
            LastMessageFromGuest = c.LastMessageFromGuest,
            UnreadCount = unreadCount,
            OrganizationName = latest?.OrganizationRef?.Name ?? latest?.Organization,
            NationalityName = guestProfile?.Nationality?.Name,
            Tier = latest?.Tier
        };
    }

    // isMine is always the CALLER's point of view, so every endpoint has to say
    // which side it is serving — the message row alone can't tell (the same row is
    // "mine" to the guest and "theirs" to the admin). It has no safe default,
    // hence no defaulted parameter: forgetting it is a compile error, not a
    // silently-false flag that renders every bubble as incoming.
    private static SupportMessageResponse MapMessage(SupportMessage m, bool isMine, string senderName = null) => new()
    {
        Id = m.PublicId,
        ConversationId = m.Conversation.PublicId,
        Body = m.Body,
        SentAt = m.SentAt,
        IsRead = m.IsRead,
        ReadAt = m.ReadAt,
        AttachmentUrl = m.AttachmentUrl,
        AttachmentType = m.AttachmentType,
        IsMine = isMine,
        SenderName = senderName ?? (m.SenderUser != null ? $"{m.SenderUser.FirstName} {m.SenderUser.LastName}".Trim() : null)
    };
}
