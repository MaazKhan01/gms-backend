using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Junction table — a guest's event participation can cover many sessions, and a
/// session can have many guests.
/// </summary>
/// <remarks>
/// Keyed on <see cref="EventGuestId"/> (the participation), not on the person: a
/// session belongs to exactly one event, so mapping it to the person would leave
/// "which event's attendance is this?" ambiguous for anyone in two events.
/// </remarks>
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
