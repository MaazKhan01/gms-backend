using System;
using Core.ViewModel.Common;

namespace Core.ViewModel.SupportChat;

// ============================================================================
// Support Chat — admin-facing conversation management DTOs. Per-message DTOs
// (SupportMessageResponse / SendSupportMessageRequest) are reused as-is from
// Core.ViewModel.VipApp — guest and admin exchange the exact same message shape.
// ============================================================================

public class SupportConversationSummaryResponse
{
    public Guid Id { get; set; }              // conversation PublicId
    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public string GuestEmail { get; set; }
    public string Status { get; set; }        // Open / Closed
    public string LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool LastMessageFromGuest { get; set; }

    // Unread count for whichever side is asking (admin's unread when returned
    // to the admin inbox, guest's unread when returned to the guest).
    public int UnreadCount { get; set; }

    // Guest attributes surfaced for the admin inbox's filter chips — same
    // fields GuestPagedRequest filters on, so the row can show what matched.
    public string OrganizationName { get; set; }
    public string NationalityName { get; set; }
    public string Tier { get; set; }
}

public class SupportConversationPagedRequest : PagedRequest
{
    public bool OnlyUnread { get; set; }
    public string Status { get; set; } // optional filter: Open / Closed

    // Same filter shape as GuestPagedRequest — public Guids resolved to
    // internal ids in SupportChatService.GetConversationsAsync.
    public Guid? OrganizationId { get; set; }
    public Guid? NationalityId { get; set; }
    public string Tier { get; set; }
}

// ============================================================================
// Driver <-> Guest chat — one new endpoint (SupportChatController.SendDriverGuestMessage)
// reusing the same SupportConversation/SupportMessage tables (Type = DriverGuest).
// Caller identity (which side is sending) comes from the auth context (ICurrentUser
// for a driver token, ICurrentGuest for a guest token) — never from the request body.
// ============================================================================
public class SendDriverGuestMessageRequest
{
    // Target a specific counterpart by their User PublicId (works for either
    // direction — a driver's or a guest's own User.PublicId).
    public Guid? RecipientUserId { get; set; }

    // Target by role instead of a specific user — only meaningful for a Guest
    // sender with RecipientRole = "driver" (resolved via that guest's
    // GuestDriverAssignment pool). A Driver sender must always target a
    // specific guest (drivers can have many guests; there's no single implicit one).
    public string RecipientRole { get; set; }

    public string Body { get; set; }
    public string AttachmentUrl { get; set; }
    public string AttachmentType { get; set; }
}
