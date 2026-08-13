using System.Collections.Generic;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

// The token dictionary every transport notification template fills its
// {placeholders} from — and the client gets as the notification's Data payload.
// One definition so wording changes in NotificationTemplates never need a
// matching edit in each service that sends a trip notification.
internal static class TransportNotificationTokens
{
    // Takes the participation, since that is what a Transport belongs to; the
    // person's name is reached through it.
    public static Dictionary<string, string> Tokens(this Transport transport, EventGuest guest) => new()
    {
        ["transportId"] = transport.PublicId.ToString(),
        ["guestName"] = $"{guest?.Guest?.FirstName} {guest?.Guest?.LastName}".Trim(),
        // Display form for the message text, ISO form for the client to re-format.
        ["pickupTime"] = transport.PickupTime?.ToString("dd MMM yyyy HH:mm") ?? "time to be confirmed",
        ["pickupTimeIso"] = transport.PickupTime?.ToString("o") ?? string.Empty,
    };
}
