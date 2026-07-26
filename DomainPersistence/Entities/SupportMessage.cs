using System;

namespace DomainPersistence.Entities;

// One guest ↔ concierge chat. ponytail: no SupportThread — a guest has exactly
// one implicit thread. Add threads only if a guest ever needs more than one.
public class SupportMessage : Entity
{
    public int GuestId { get; set; }
    public string Body { get; set; }
    public bool FromGuest { get; set; }   // true = guest → concierge, false = concierge → guest
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Nullable so the migration doesn't need a hard NOT NULL backfill; every new
    // write always sets it (backfilled for pre-existing rows in the migration).
    public int? ConversationId { get; set; }

    // Which admin sent it, when FromGuest == false. Null for guest-authored messages.
    public int? SenderUserId { get; set; }

    // Future attachment support — unused today.
    public string AttachmentUrl { get; set; }
    public string AttachmentType { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual SupportConversation Conversation { get; set; }
    public virtual User SenderUser { get; set; }
}
