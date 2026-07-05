using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Junction table — a guest can attend many sessions, a session can have many guests.
/// </summary>
public class GuestSession
{
    public Guid GuestId { get; set; }
    public Guid SessionId { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual Session Session { get; set; }
}
