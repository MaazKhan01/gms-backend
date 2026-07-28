using System;
using System.Collections.Generic;
using Core.ViewModel.Common;

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
// A guest can hold more than one flight/hotel/transport booking. Each Input's
// Id says which specific booking to update in place; leave it null/empty to
// add a new one instead. Services' "New Booking" always omits Id (append);
// the per-booking edit flows fetch a booking with its Id already populated
// so saving it back updates just that record.
public class GuestTravelRequest
{
    public FlightInput Flight { get; set; }
    public AccommodationInput Accommodation { get; set; }
    public TransportInput Transport { get; set; }
}

public class FlightInput
{
    public Guid? Id { get; set; }
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
    public Guid? Id { get; set; }
    public Guid HotelId { get; set; }
    public Guid? RoomTypeId { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
}

public class TransportInput
{
    public Guid? Id { get; set; }
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

// ── Arrivals & Departures ────────────────────────────────────────────────────
// Guest-centric: one row pairs a guest's inbound flights with their outbound
// ones, so staff can read arrival and departure off a single line. Deliberately
// separate from EventFlightRow (and its own endpoint) so this view can be
// permission-gated on its own later without touching the Flights tab.
public class ArrivalsDeparturesRequest : PagedRequest
{
    /// <summary>"all" (default), "inbound" or "outbound".</summary>
    public string Direction { get; set; }

    /// <summary>Inclusive date window on the flight's legs. Either end may be
    /// omitted for an open-ended range; set both to the same day to filter to
    /// a single date.</summary>
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class ArrivalDepartureRow
{
    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public string Email { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }

    // A guest may hold more than one booking in either direction.
    public List<ArrivalDepartureFlight> Inbound { get; set; } = [];
    public List<ArrivalDepartureFlight> Outbound { get; set; } = [];
}

public class ArrivalDepartureFlight
{
    public Guid Id { get; set; }
    public string FlightNumber { get; set; }
    /// <summary>The raw FlightType name, so the UI can show "Return" as-is.</summary>
    public string FlightType { get; set; }
    public string DepartureCode { get; set; }
    public string DepartureCity { get; set; }
    public string ArrivalCode { get; set; }
    public string ArrivalCity { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int LegCount { get; set; }
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
    public Guid Id { get; set; }
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
    public Guid Id { get; set; }
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
