using System;

namespace DomainPersistence.Entities;

// One message in a SupportConversation. UserId always identifies the guest
// side of the thread (see SupportConversation.Type) — it is NOT the sender;
// use SenderUserId for that. FromGuest is true when the sender is the
// conversation's own UserId (guest side), false when the sender is the other
// side (an admin for AdminSupport, the driver for DriverGuest) — kept as a
// stored bool rather than derived so the guest app's existing "which side is
// this bubble on" field never has to change shape.
public class SupportMessage : Entity
{
    public int UserId { get; set; }
    public string Body { get; set; }
    public bool FromGuest { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Nullable so the migration doesn't need a hard NOT NULL backfill; every new
    // write always sets it (backfilled for pre-existing rows in the migration).
    public int? ConversationId { get; set; }

    // Who actually sent it. Null only for guest-authored AdminSupport messages
    // (kept for backward compatibility with existing rows/clients); every new
    // write — guest or otherwise — sets it once FromGuest is derived from it above.
    public int? SenderUserId { get; set; }

    // Future attachment support — unused today.
    public string AttachmentUrl { get; set; }
    public string AttachmentType { get; set; }

    public virtual User User { get; set; }
    public virtual SupportConversation Conversation { get; set; }
    public virtual User SenderUser { get; set; }
}
