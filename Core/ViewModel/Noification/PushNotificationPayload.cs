using System.Collections.Generic;

namespace Core.ViewModel.Noification;

public enum NotificationRecipientType
{
    User,  // admin/staff — DomainPersistence.Entities.User
    Guest  // VIP app guest — DomainPersistence.Entities.Guest
}

// Provider-agnostic push payload. IPushNotificationProvider implementations
// (manual today, Firebase later) consume this without the caller knowing which
// transport is behind it.
public class PushNotificationPayload
{
    public NotificationRecipientType RecipientType { get; set; }

    // Internal id of the recipient — UserId when RecipientType == User, GuestId when Guest.
    public int? UserId { get; set; }
    public int? GuestId { get; set; }

    public string Title { get; set; }
    public string Body { get; set; }

    // SignalR method/topic name (see Core.Constants.RealtimeTopics) used by the
    // manual provider today; a future Firebase provider can ignore it and rely
    // on Data instead.
    public string Topic { get; set; }

    public IDictionary<string, string> Data { get; set; }
}
