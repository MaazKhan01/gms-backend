using System.Collections.Generic;

namespace Core.ViewModel.Noification;

// Recipient-agnostic notification content — the payload half of a send.
// INotificationManagerService pairs this with a targeting strategy (one user,
// many users, a role, a permission, everyone, or a guest) to persist +
// realtime-push the resulting Notification/GuestNotification row(s).
public class NotificationContent
{
    public string Title { get; set; }
    public string Message { get; set; }

    // Free-form category/template code (e.g. "booking-created", "support_message").
    // See Core.Constants.Notification.NotificationTemplateCodes for examples.
    public string Type { get; set; }

    public string RedirectUrl { get; set; }

    // Round-tripped to the client as-is (serialized to JSON on the Notification
    // row's Data column); also forwarded as the SignalR payload's Data.
    public IDictionary<string, string> Data { get; set; }

    // SignalR method/topic name to invoke on clients. Defaults to
    // RealtimeTopics.NotificationNew (the generic bell channel) when null —
    // set explicitly to preserve an existing feature-specific topic name
    // (e.g. RealtimeTopics.SupportMessageNew) for callers migrating from a
    // hand-rolled push to this shared dispatcher without changing their wire contract.
    public string Topic { get; set; }
}
