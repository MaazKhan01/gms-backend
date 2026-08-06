using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Constants;

/// <summary>
/// Existing lookup tables a service form field may draw its options from.
///
/// A whitelist rather than a free-text endpoint: an admin picks "Airports" from
/// a dropdown, so there is no URL to mistype and no way to point a form at an
/// arbitrary route. Adding a source here is a one-line change and needs no
/// database migration.
///
/// The stored value is the lookup row's <c>PublicId</c>, never its label, so
/// renaming an airport or a hotel leaves completed bookings intact — this is
/// what keeps referential meaning after the move to JSON values.
/// See docs/service-levels-v2.md.
/// </summary>
public class ServiceLookupSource
{
    public string Key { get; init; }
    public string Label { get; init; }
    public string LabelAr { get; init; }

    /// <summary>Path the client calls to populate the dropdown.</summary>
    public string Endpoint { get; init; }
}

public static class ServiceLookupSources
{
    public const string Airports = "airports";
    public const string Hotels = "hotels";
    public const string RoomTypes = "roomTypes";
    public const string FlightClasses = "flightClasses";
    public const string Locations = "locations";
    public const string Vehicles = "vehicles";
    public const string VehicleTypes = "vehicleTypes";
    public const string Drivers = "drivers";
    public const string Nationalities = "nationalities";
    public const string Organizations = "organizations";

    public static readonly IReadOnlyList<ServiceLookupSource> All = new List<ServiceLookupSource>
    {
        new() { Key = Airports,      Label = "Airports",       LabelAr = "المطارات",        Endpoint = "/v1/lookups/airports" },
        new() { Key = Hotels,        Label = "Hotels",         LabelAr = "الفنادق",         Endpoint = "/v1/lookups/hotels" },
        new() { Key = RoomTypes,     Label = "Room types",     LabelAr = "أنواع الغرف",     Endpoint = "/v1/lookups/room-types" },
        new() { Key = FlightClasses, Label = "Flight classes", LabelAr = "درجات الطيران",   Endpoint = "/v1/lookups/flight-classes" },
        new() { Key = Locations,     Label = "Locations",      LabelAr = "المواقع",         Endpoint = "/v1/lookups/locations" },
        new() { Key = Vehicles,      Label = "Vehicles",       LabelAr = "المركبات",        Endpoint = "/v1/vehicles" },
        new() { Key = VehicleTypes,  Label = "Vehicle types",  LabelAr = "أنواع المركبات",  Endpoint = "/v1/lookups/vehicle-types" },
        new() { Key = Drivers,       Label = "Drivers",        LabelAr = "السائقون",        Endpoint = "/v1/lookups/drivers" },
        new() { Key = Nationalities, Label = "Nationalities",  LabelAr = "الجنسيات",        Endpoint = "/v1/nationality" },
        new() { Key = Organizations, Label = "Organisations",  LabelAr = "المؤسسات",        Endpoint = "/v1/organizations"      },
    };

    public static bool IsValid(string key)
        => !string.IsNullOrWhiteSpace(key)
           && All.Any(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
}
