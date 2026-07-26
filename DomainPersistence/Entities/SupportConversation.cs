using System;

namespace DomainPersistence.Entities;

// One row per guest — the persisted support chat thread/summary. A guest has
// exactly one implicit conversation (matches SupportMessage's one-thread-per-guest
// design). Kept separate from SupportMessage so the admin inbox (list guests,
// unread-first, last message preview) is a fast indexed read instead of a
// GroupBy over every message on every request.
public class SupportConversation : Entity
{
    public int GuestId { get; set; }
    public string Status { get; set; } // Open / Closed — see Core.Constants.SupportChatStatuses

    // Future multi-admin hook: which admin owns this conversation. Null = unassigned/any admin.
    public int? AssignedAdminUserId { get; set; }

    public DateTime? LastMessageAt { get; set; }
    public string LastMessagePreview { get; set; }
    public bool LastMessageFromGuest { get; set; }

    public int UnreadByAdminCount { get; set; }
    public int UnreadByGuestCount { get; set; }

    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual User AssignedAdmin { get; set; }
    public virtual User ClosedByUser { get; set; }
}
