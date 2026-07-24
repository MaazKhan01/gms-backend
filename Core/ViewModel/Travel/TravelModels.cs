using System;
using System.Collections.Generic;

namespace Core.ViewModel.Travel;

// ---- Dropdown lookups (one call populates the whole wizard step) ----
public class TravelLookupsResponse
{
    public List<IdNameDto> FlightTypes { get; set; } = new();
    public List<IdNameDto> FlightClasses { get; set; } = new();
    public List<IdNameDto> RoomTypes { get; set; } = new();
    public List<HotelDto> Hotels { get; set; } = new();
    public List<LocationDto> Locations { get; set; } = new();
}
public class IdNameDto { public Guid Id { get; set; } public string Name { get; set; } }
public class HotelDto { public Guid Id { get; set; } public string Name { get; set; } public string Address { get; set; } }
public class LocationDto { public Guid Id { get; set; } public string Address { get; set; } }

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
    public string DepartureCode { get; set; }
    public string DepartureCity { get; set; }
    public string ArrivalCode { get; set; }
    public string ArrivalCity { get; set; }
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
    public string VehicleType { get; set; }
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
