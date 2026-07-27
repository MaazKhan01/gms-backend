using System;
using System.Collections.Generic;

namespace Core.ViewModel.Noification;

// Admin-facing "send a notification" request (POST /api/v1/notifications/send).
// Exactly one targeting field should be set — NotificationService validates this
// and resolves it down to internal ids before handing off to
// INotificationManagerService, which is the reusable, internal-id-based engine.
//
// Users and Guests are distinct audiences (guests authenticate via OTP, are not
// rows in the Users table, and receive GuestNotification rows over a separate
// SignalR group) — hence the separate GuestIds/BroadcastGuests targets rather
// than one generic "recipient id" field.
public class SendNotificationRequest
{
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
    public string RedirectUrl { get; set; }
    public IDictionary<string, string> Data { get; set; }

    // Targeting — set exactly one of the six below.

    // Users (admin/staff)
    public List<Guid> UserIds { get; set; }
    public string RoleCode { get; set; }
    public string PermissionCode { get; set; }
    public bool Broadcast { get; set; } // every active User

    // Guests (VIP app, OTP login)
    public List<Guid> GuestIds { get; set; }
    public bool BroadcastGuests { get; set; } // every Guest with NotificationsEnabled
}
