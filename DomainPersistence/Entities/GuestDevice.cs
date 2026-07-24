using System;

namespace DomainPersistence.Entities;

// Push-notification target. One row per device the guest logs in from.
public class GuestDevice : Entity
{
    public int GuestId { get; set; }
    public string Token { get; set; }      // FCM / APNs token
    public string Platform { get; set; }   // ios | android
    public DateTime? LastSeenAt { get; set; }

    public virtual Guest Guest { get; set; }
}
