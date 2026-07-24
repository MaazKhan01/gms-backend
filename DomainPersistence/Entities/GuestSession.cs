using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Junction table — a guest can attend many sessions, a session can have many guests.
/// </summary>
public class GuestSession
{
    public int GuestId { get; set; }
    public int SessionId { get; set; }

    // Selection state from the "Select Event" screen. Null/"selected" = picked,
    // "confirmed" once staff confirm, "declined" if the guest opted out.
    public string Status { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual Session Session { get; set; }
}
