namespace Core.Constants
{
    // SignalR method/topic names invoked on clients — see IRealTimeAlertService.
    public static class RealtimeTopics
    {
        public const string SupportMessageNew = "support-message-new";
        public const string SupportConversationRead = "support-conversation-read";

        // Generic in-app notification channel. Every Notification/GuestNotification
        // row pushes here so a single client listener can drive the notification
        // bell regardless of which feature generated it (see NotificationManagerService).
        public const string NotificationNew = "notification-new";

        // Pushed after any mutation that changes a user's unread count (mark-read,
        // mark-all-read, delete) so the badge stays in sync across open tabs/devices.
        public const string NotificationCountChanged = "notification-count-changed";

        // ── Transportation module ────────────────────────────────────────
        // Guest-facing
        public const string TransportationDriverAssigned = "transportation-driver-assigned";
        public const string TransportationRequestAccepted = "transportation-request-accepted";
        public const string TransportationDriverArrived = "transportation-driver-arrived";
        public const string TransportationRideStarted = "transportation-ride-started";
        public const string TransportationRideCompleted = "transportation-ride-completed";
        public const string TransportationRideCancelled = "transportation-ride-cancelled";
        // Driver-facing
        public const string TransportationRequestAvailable = "transportation-request-available";
        public const string TransportationRequestUnavailable = "transportation-request-unavailable";
        public const string TransportationScheduleUpdated = "transportation-schedule-updated";
    }
}
