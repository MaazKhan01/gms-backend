using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Core.Constants;

/// <summary>Services a guest may REQUEST from the VIP app themselves. Stored on
/// Guest.AllowedServicesJson as a JSON array of these int values.</summary>
public enum GuestServiceType
{
    Flight = 1,
    Accommodation = 2,
    Transport = 3,
}

// Guest.AllowedServicesJson is a permission list, not a booking list: an admin
// who booked nothing for a guest can still tick "transport", and the app then
// shows that guest the request screen with an empty itinerary. One JSON column
// instead of a bool per service — adding a fourth service is one enum member.
//
// ponytail: raw JSON string on the entity, parsed in memory. A queryable shape
// (join table, or SQL Server JSON_VALUE) only pays off if we ever need to filter
// guests BY allowed service; nothing does today.
public static class GuestServices
{
    /// <summary>The allowed service values, ignoring anything unrecognised.
    /// Never throws and never returns null — bad/legacy JSON reads as "nothing
    /// allowed", which is the safe direction for a permission list.</summary>
    public static List<int> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<int>();

        try
        {
            return (JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>())
                .Where(IsDefined)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }
        catch (JsonException)
        {
            return new List<int>();
        }
    }

    public static bool Allows(string json, GuestServiceType service)
        => Parse(json).Contains((int)service);

    /// <summary>JSON for storage — null when nothing is allowed, so the column
    /// stays empty rather than holding "[]".</summary>
    public static string Serialize(IEnumerable<int> values)
    {
        var clean = (values ?? Enumerable.Empty<int>())
            .Where(IsDefined)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }

    private static bool IsDefined(int value) => Enum.IsDefined(typeof(GuestServiceType), value);
}
