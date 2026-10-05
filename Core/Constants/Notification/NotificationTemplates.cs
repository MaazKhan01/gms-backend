using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Core.ViewModel.Noification;

namespace Core.Constants.Notification;

// The one place every notification's wording lives. A feature that needs to
// notify someone adds ONE entry here and calls
// INotificationManagerService.SendTo*Async(recipient, code, tokens) — no inline
// title/message strings scattered across services, no per-feature notifier class.
//
// Title/Message/RedirectUrl may contain {token} placeholders; the tokens
// dictionary passed at send time fills them and is ALSO handed to the client as
// the notification's Data payload (so the same dict drives both the text and
// what the app needs to deep-link). Any placeholder with no matching token is
// dropped, so a caller that omits an optional token still sends clean text.
//
// Topic = the SignalR event name clients listen on. Null → the generic
// notification bell channel (RealtimeTopics.NotificationNew); set it only when a
// client already listens on a feature-specific event.
public sealed record NotificationTemplate(
    string Title,
    string Message,
    string Topic = null,
    string RedirectUrl = null);

public static class NotificationTemplates
{
    // ── Codes (also stored as Notification.Type, so keep them stable) ──────
    // Transportation — driver-facing
    public const string TransportDriverAssigned = "transport-driver-assigned";
    public const string TransportDriverUnassigned = "transport-driver-unassigned";
    public const string TransportDriverTripCancelled = "transport-driver-trip-cancelled";
    // Transportation — guest-facing. These used to be fire-and-forget SignalR
    // pushes; they're templates now so they persist and reach FCM too (the
    // mobile apps have no socket — see FirebaseNotificationProvider).
    public const string TransportGuestDriverArrived = "transport-guest-driver-arrived";
    public const string TransportGuestRideStarted = "transport-guest-ride-started";
    public const string TransportGuestRideCompleted = "transport-guest-ride-completed";
    public const string TransportGuestRideCancelled = "transport-guest-ride-cancelled";
    // Support chat
    public const string SupportMessageNew = "support_message";
    public const string SupportReplyNew = "support_reply";
    public const string DriverGuestMessage = "driver_guest_message";
    // On-mission ops — the coordinator's broadcast to the delegation on the
    // ground. The wording is the coordinator's, so the template is a shell.
    public const string MissionGatheringNotice = "mission-gathering-notice";

    private static readonly Dictionary<string, NotificationTemplate> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        [TransportDriverAssigned] = new(
            "New trip assigned",
            "Pickup for {guestName} — {pickupTime}.",
            RealtimeTopics.TransportationScheduleUpdated),

        [TransportDriverUnassigned] = new(
            "Trip removed",
            "The trip for {guestName} is no longer assigned to you.",
            RealtimeTopics.TransportationScheduleUpdated),

        [TransportDriverTripCancelled] = new(
            "Trip cancelled",
            "The trip for {guestName} — {pickupTime} — has been cancelled.",
            RealtimeTopics.TransportationRideCancelled),

        // Topics match what the portal already listens on, so migrating these
        // off the raw SignalR calls doesn't change the wire contract.
        [TransportGuestDriverArrived] = new(
            "Your driver has arrived",
            "Your driver is waiting at the pickup point.",
            RealtimeTopics.TransportationDriverArrived,
            "/rides/{transportId}"),

        [TransportGuestRideStarted] = new(
            "Ride started",
            "You're on your way.",
            RealtimeTopics.TransportationRideStarted,
            "/rides/{transportId}"),

        [TransportGuestRideCompleted] = new(
            "Ride completed",
            "You've arrived at your destination.",
            RealtimeTopics.TransportationRideCompleted,
            "/rides/{transportId}"),

        [TransportGuestRideCancelled] = new(
            "Ride cancelled",
            "Your transportation booking has been cancelled.",
            RealtimeTopics.TransportationRideCancelled,
            "/rides/{transportId}"),

        [SupportMessageNew] = new(
            "New support message",
            "{preview}",
            RealtimeTopics.SupportMessageNew,
            "/admin/support-chat/{conversationId}"),

        [SupportReplyNew] = new(
            "New reply from support",
            "{preview}",
            RealtimeTopics.SupportMessageNew,
            "/support/{conversationId}"),

        [DriverGuestMessage] = new(
            "New message",
            "{preview}",
            RealtimeTopics.SupportMessageNew,
            "/chat/{conversationId}"),

        [MissionGatheringNotice] = new(
            "Message from your delegation",
            "{message}"),
    };

    // Unknown code → a content object carrying the code as its own title, so a
    // typo shows up as a visibly odd notification instead of a 500 mid-request
    // (the notification is never the point of the operation that triggered it).
    public static NotificationContent Build(string code, IDictionary<string, string> tokens = null)
    {
        if (!Catalog.TryGetValue(code ?? string.Empty, out var t))
            t = new NotificationTemplate(code ?? "Notification", null);

        return new NotificationContent
        {
            Title = Fill(t.Title, tokens),
            Message = Fill(t.Message, tokens),
            RedirectUrl = Fill(t.RedirectUrl, tokens),
            Type = code,
            Topic = t.Topic,
            Data = tokens,
        };
    }

    private static readonly Regex LeftoverToken = new(@"\{\w+\}", RegexOptions.Compiled);

    private static string Fill(string template, IDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var sb = new StringBuilder(template);
        if (tokens != null)
            foreach (var kv in tokens)
                sb.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);

        return LeftoverToken.Replace(sb.ToString(), string.Empty).Trim();
    }
}
