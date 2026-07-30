using System;

namespace DomainPersistence.Entities;

// Push-notification target — one row per device a User (staff, driver, or
// guest — all three are Users now) has logged in from. Replaces the old
// Guest-only GuestDevice table: notification delivery no longer forks by
// audience, so the device table doesn't either.
public class Device : Entity
{
    public int UserId { get; set; }

    // FCM token. Unique — a token upsert re-points it at whichever User most
    // recently registered it (a device only ever belongs to one signed-in
    // User at a time), matching the previous GuestDevice upsert behavior.
    public string Token { get; set; }

    public string Platform { get; set; } // ios | android | web

    // Client-supplied device identifier (distinct from the FCM token, which
    // rotates) — lets a client reconcile/replace its own prior registration
    // (e.g. after a token refresh) without depending on the old token value.
    public string DeviceIdentifier { get; set; }
    public string DeviceModel { get; set; }
    public string OsVersion { get; set; }
    public string AppVersion { get; set; }

    // Per-device opt-out — independent of Guest.NotificationsEnabled (a
    // broadcast-level preference) and of the User's own notification reads.
    public bool NotificationsEnabled { get; set; } = true;

    // False once FCM reports the token as invalid/unregistered, or the client
    // explicitly deregisters on logout — excluded from send fan-out without
    // losing the row's history.
    public bool IsActive { get; set; } = true;

    public DateTime? LastActiveAt { get; set; }

    // Set whenever Token is (re)written — distinct from LastActiveAt, which a
    // client can bump on every foreground/heartbeat without the token itself changing.
    public DateTime? TokenUpdatedAt { get; set; }

    public virtual User User { get; set; }
}
