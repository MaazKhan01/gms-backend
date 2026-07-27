using System;
using System.Collections.Generic;

namespace Core.ViewModel.Travel;

// ---- Dropdown lookup DTOs (one GET endpoint per list) ----
public class IdNameDto { public Guid Id { get; set; } public string Name { get; set; } }
public class AirportDto { public Guid Id { get; set; } public string Code { get; set; } public string City { get; set; } public string Country { get; set; } public string Continent { get; set; } public Guid? LocationId { get; set; } }
public class HotelDto { public Guid Id { get; set; } public string Name { get; set; } public string Address { get; set; } public Guid? LocationId { get; set; } }
public class LocationDto { public Guid Id { get; set; } public string Address { get; set; } public string Type { get; set; } public string Longitude { get; set; } public string Latitude { get; set; } }

// ---- Create wizard-dropdown lookup records (admin-managed) ----
public class CreateNamedLookupRequest { public string Name { get; set; } }
public class CreateHotelRequest { public string Name { get; set; } public string Address { get; set; } public Guid? LocationId { get; set; } }
public class CreateAirportRequest { public string Code { get; set; } public string City { get; set; } public string Country { get; set; } public string Continent { get; set; } public Guid? LocationId { get; set; } }
// Same shape for create and edit.
public class LocationRequest { public string Address { get; set; } public string Type { get; set; } public string Longitude { get; set; } public string Latitude { get; set; } }

// ---- Per-guest travel: any subset of the three may be present ----
public class GuestTravelRequest
{
    public FlightInput Flight { get; set; }
    public AccommodationInput Accommodation { get; set; }
    public TransportInput Transport { get; set; }
}

public class FlightInput
{
    public Guid FlightTypeId { get; set; }
    public Guid? FlightClassId { get; set; }
    public string Status { get; set; }
    public string Seat { get; set; }
    // single leg (MVP)
    public string FlightNumber { get; set; }
    public Guid? FromAirportId { get; set; }
    public Guid? ToAirportId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class AccommodationInput
{
    public Guid HotelId { get; set; }
    public Guid? RoomTypeId { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
}

public class TransportInput
{
    public Guid? PickupLocationId { get; set; }
    public Guid? DropoffLocationId { get; set; }
    public Guid? VehicleTypeId { get; set; }
    public string TripStatus { get; set; }
    public Guid? DriverId { get; set; }   // DriverProfile public id (GET /lookups/drivers)
    public DateTime? PickupTime { get; set; }
    public DateTime? EstimatedArrival { get; set; }
}

// ---- Get (prefill edit): echoes inputs (public guids) + display names ----
public class GuestTravelResponse
{
    public FlightInput Flight { get; set; }
    public AccommodationInput Accommodation { get; set; }
    public TransportInput Transport { get; set; }
}

// ---- Admin travel view: one row list per tab, scoped to an event ----
// Only fields that actually live in the tables — nulls where a guest has none.
public class EventFlightRow
{
    public Guid Id { get; set; }
    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }
    public string Status { get; set; }
    public string FlightType { get; set; }
    public string FlightClass { get; set; }
    public string Seat { get; set; }

    // Itinerary summary — first/last leg, for the collapsed table row.
    public string FlightNumber { get; set; }
    public string DepartureCode { get; set; }
    public string DepartureCity { get; set; }
    public string ArrivalCode { get; set; }
    public string ArrivalCity { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int LegCount { get; set; }

    public List<FlightLegRow> Legs { get; set; } = [];
}

public class FlightLegRow
{
    public Guid Id { get; set; }
    public string FlightNumber { get; set; }
    public string DepartureCode { get; set; }
    public string DepartureCity { get; set; }
    public string DepartureCountry { get; set; }
    public string ArrivalCode { get; set; }
    public string ArrivalCity { get; set; }
    public string ArrivalCountry { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class EventAccommodationRow
{
    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }
    public string Hotel { get; set; }
    public string RoomType { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
}

public class EventTransportRow
{
    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }
    public string VehicleType { get; set; }
    public Guid? DriverId { get; set; }
    public string DriverName { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }
    public DateTime? PickupTime { get; set; }
    public string TripStatus { get; set; }
}
