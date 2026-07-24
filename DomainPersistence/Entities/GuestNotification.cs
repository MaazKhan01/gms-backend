using System;

namespace DomainPersistence.Entities;

// Guest-facing notification (the bell on Home). Separate from Notification,
// which is user/admin-scoped (Notification.UserId → User).
public class GuestNotification : Entity
{
    public int GuestId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
    public bool Read { get; set; }
    public string RedirectUrl { get; set; }

    public virtual Guest Guest { get; set; }
}
