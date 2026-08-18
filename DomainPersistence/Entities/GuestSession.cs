using System;

namespace DomainPersistence.Entities;

public class GuestSession
{
    public int EventGuestId { get; set; }
    public int SessionId { get; set; }

    // Selection state from the "Select Event" screen. Null/"selected" = picked,
    // "confirmed" once staff confirm, "declined" if the guest opted out.
    public string Status { get; set; }

    public virtual EventGuest EventGuest { get; set; }
    public virtual Session Session { get; set; }
}
