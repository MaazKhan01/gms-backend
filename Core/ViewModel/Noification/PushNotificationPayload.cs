using System.Collections.Generic;

namespace Core.ViewModel.Noification;

// Provider-agnostic push payload. IPushNotificationProvider implementations
// (manual/SignalR, Firebase/FCM) consume this without the caller knowing which
// transport is behind it. Every recipient — staff, driver, or guest — is a
// User now, so there's no longer a separate recipient-type/id pair to carry.
public class PushNotificationPayload
{
    public int UserId { get; set; }

    public string Title { get; set; }
    public string Body { get; set; }

    // SignalR method/topic name (see Core.Constants.RealtimeTopics) used by the
    // manual provider today; a future Firebase provider can ignore it and rely
    // on Data instead.
    public string Topic { get; set; }

    public IDictionary<string, string> Data { get; set; }

    // Rich payload for the in-app SignalR channel (typically the full
    // NotificationResponse/GuestNotificationResponse) — sent instead of Data
    // when present, so the client can render the notification immediately
    // without a round-trip. Push-only providers (Firebase/APNs, which require
    // string-only data) should ignore this and use Data instead.
    public object Payload { get; set; }
}
