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
    }
}
