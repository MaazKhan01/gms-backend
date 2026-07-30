using System;

namespace DomainPersistence.Entities;

// One conversation thread. Two products share this table (see Type):
//   AdminSupport — a guest's single implicit thread with the concierge/admin
//                  team. Exactly one per guest (UserId), OtherUserId is null.
//   DriverGuest  — one thread per (guest, driver) pair. UserId is always the
//                  guest side, OtherUserId the driver side, so "UnreadByAdminCount"/
//                  "UnreadByGuestCount" below keep meaning "unread for the other
//                  party" / "unread for the User-side party" across both types
//                  without needing a third pair of counters.
public class SupportConversation : Entity
{
    public int UserId { get; set; }
    public string Type { get; set; } // AdminSupport / DriverGuest — see Core.Constants.SupportChatTypes
    public string Status { get; set; } // Open / Closed — see Core.Constants.SupportChatStatuses

    // Set only for Type == DriverGuest — the driver's User.Id. Null for AdminSupport,
    // where "the other side" is any admin holding SupportChatManage, not one fixed User.
    public int? OtherUserId { get; set; }

    // Future multi-admin hook: which admin owns this conversation. Null = unassigned/any admin.
    public int? AssignedAdminUserId { get; set; }

    public DateTime? LastMessageAt { get; set; }
    public string LastMessagePreview { get; set; }
    public bool LastMessageFromGuest { get; set; }

    public int UnreadByAdminCount { get; set; }
    public int UnreadByGuestCount { get; set; }

    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }

    public virtual User User { get; set; }
    public virtual User OtherUser { get; set; }
    public virtual User AssignedAdmin { get; set; }
    public virtual User ClosedByUser { get; set; }
}
