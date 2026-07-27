using System;
using System.Collections.Generic;

namespace Core.ViewModel.Travel;

// ---- Dropdown lookup DTOs (one GET endpoint per list) ----
public class IdNameDto { public Guid Id { get; set; } public string Name { get; set; } }
public class AirportDto { public Guid Id { get; set; } public string Code { get; set; } public string AirportName { get; set; } public Guid? LocationId { get; set; } }
public class HotelDto { public Guid Id { get; set; } public string Name { get; set; } public string Address { get; set; } public Guid? LocationId { get; set; } }
public class LocationDto { public Guid Id { get; set; } public string Address { get; set; } public string Type { get; set; } }

// ---- Create wizard-dropdown lookup records (admin-managed) ----
public class CreateNamedLookupRequest { public string Name { get; set; } }
public class CreateHotelRequest { public string Name { get; set; } public string Address { get; set; } public Guid? LocationId { get; set; } }
public class CreateAirportRequest { public string Code { get; set; } public string AirportName { get; set; } public Guid? LocationId { get; set; } }

// ---- Per-guest travel: any subset of the three may be present ----
public class GuestTravelRequest
{
    public FlightInput Flight { get; set; }
    public AccommodationInput Accommodation { get; set; }
    public TransportInput Transport { get; set; }
    // false (default) = replace the guest's existing section(s) — used by the
    // guest wizard's edit-in-place flow. true = add alongside whatever the
    // guest already has — used by Services' "New Booking" (a guest can hold
    // more than one flight/hotel/transport booking).
    public bool Append { get; set; }
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
    public string RoomView { get; set; }
    public int? GuestCount { get; set; }
    public string ConciergeName { get; set; }
    public string ConciergePhone { get; set; }
}

public class TransportInput
{
    public Guid? PickupLocationId { get; set; }
    public Guid? DropoffLocationId { get; set; }
    public Guid? VehicleTypeId { get; set; }
    public string Plate { get; set; }
    public string TripStatus { get; set; }
    public string DriverName { get; set; }
    public string DriverPhone { get; set; }
    public double? DriverRating { get; set; }
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
    public string FlightNumber { get; set; }
    public string DepartureCode { get; set; }
    public string ArrivalCode { get; set; }
    public DateTime? Date { get; set; }
    public string Status { get; set; }
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
    public string DriverName { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }
    public DateTime? PickupTime { get; set; }
    public string TripStatus { get; set; }
}
