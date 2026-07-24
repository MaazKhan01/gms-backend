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

    public virtual Guest Guest { get; set; }
}
