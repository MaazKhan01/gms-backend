using System;

namespace DomainPersistence.Entities;

// User/admin-scoped in-app notification. See GuestNotification for the
// guest-facing (VIP app) equivalent — kept as a separate table because Guest
// and User internal ids can collide and the two audiences have distinct
// client apps/lifecycles. Inherits Entity/AuditEntity so it gets the same
// soft-delete + audit trail as the rest of the domain (see AuditInterceptor
// and the global query-filter convention in ApplicationDBContext).
public class Notification : Entity
{
    public int UserId { get; set; }
    public string Title { get; set; }
    public string? Message { get; set; }
    public string? Type { get; set; }
    public bool? Read { get; set; }
    public string? RedirectUrl { get; set; }

    // Optional JSON payload for extensibility (deep-link params, related
    // entity ids, etc.) — opaque to the server, round-tripped to the client.
    public string? Data { get; set; }

    public virtual User User { get; set; }
}
