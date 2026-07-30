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
