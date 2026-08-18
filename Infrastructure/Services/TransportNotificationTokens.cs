using System.Collections.Generic;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

// The token dictionary every transport notification template fills its
// {placeholders} from — and the client gets as the notification's Data payload.
// One definition so wording changes in NotificationTemplates never need a
// matching edit in each service that sends a trip notification.
internal static class TransportNotificationTokens
{
    // `participation` carries both the event context and, through Guest, the
    // person's name — callers must not pass an EventGuest id where a Guest id is
    // expected, so nothing here exposes a bare "guestId".
    public static Dictionary<string, string> Tokens(this Transport transport, EventGuest participation) => new()
    {
        ["transportId"] = transport.PublicId.ToString(),
        ["eventGuestId"] = participation?.PublicId.ToString() ?? string.Empty,
        ["eventId"] = participation?.Event?.PublicId.ToString() ?? string.Empty,
        ["guestName"] = $"{participation?.Guest?.FirstName} {participation?.Guest?.LastName}".Trim(),
        // Display form for the message text, ISO form for the client to re-format.
        ["pickupTime"] = transport.PickupTime?.ToString("dd MMM yyyy HH:mm") ?? "time to be confirmed",
        ["pickupTimeIso"] = transport.PickupTime?.ToString("o") ?? string.Empty,
    };
}
