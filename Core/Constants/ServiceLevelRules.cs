using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Core.Constants;

/// <summary>Guest fields a Service Level may require before a guest can be
/// placed on it. Keys match the camelCase names the frontend already uses on the
/// guest form, so the UI can highlight the offending field directly.</summary>
public static class GuestRequirableFields
{
    public const string Email = "email";
    public const string NationalityId = "nationalityId";
    public const string OrganizationId = "organizationId";
    public const string PhotoUrl = "photoUrl";
    public const string ArrivalDate = "arrivalDate";
    public const string DepartureDate = "departureDate";

    public static readonly string[] All =
        { Email, NationalityId, OrganizationId, PhotoUrl, ArrivalDate, DepartureDate };

    /// <summary>Human label for validation messages.</summary>
    public static string Label(string key) => key switch
    {
        Email => "Email",
        NationalityId => "Nationality",
        OrganizationId => "Organization",
        PhotoUrl => "Photo",
        ArrivalDate => "Arrival date",
        DepartureDate => "Departure date",
        _ => key,
    };

    public static bool IsValid(string key) => All.Contains(key);
}

/// <summary>Parse/serialize for <c>ServiceLevel.RequiredGuestFieldsJson</c>.
/// Same defensive contract as <see cref="GuestServices"/> — bad JSON reads as
/// "nothing required", which is the safe direction for a gate that can already
/// be overridden.</summary>
public static class ServiceLevelRules
{
    public static List<string> ParseRequiredFields(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>())
                .Where(GuestRequirableFields.IsValid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    public static string SerializeRequiredFields(IEnumerable<string> keys)
    {
        var clean = (keys ?? Enumerable.Empty<string>())
            .Where(GuestRequirableFields.IsValid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }
}
