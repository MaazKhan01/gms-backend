using System;
using System.Collections.Generic;

namespace Core.ViewModel.VipApp;

// ============================================================================
// VIP Guest App — view models (mobile-facing).
// One file on purpose: these are thin DTOs for a single controller. Split only
// if a group starts growing its own logic. ponytail: flat until it hurts.
// ============================================================================

// ---------------- Auth ----------------
public record RequestOtpRequest(string Email);
public record VerifyOtpRequest(string Email, string Code);
public record RefreshTokenRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

public class GuestAuthResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public GuestProfileResponse Guest { get; set; }
}

// ---------------- Events / selection ----------------
public class GuestEventResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }        // Sports / Conference (chip)
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string ImageUrl { get; set; }
    public string Status { get; set; }
}

public class GuestSessionResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Title { get; set; }
    public string Category { get; set; }     // Sports / Conference
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string VenueName { get; set; }
    public string SelectionStatus { get; set; } // selected / confirmed / declined
}

// Multi-select confirm/decline from the "Select Event" screen.
public record SessionSelectionRequest(List<Guid> SessionIds, bool Decline);

// ---------------- Agenda ----------------
public class AgendaCardResponse
{
    public string Flag { get; set; }        // UPCOMING FLIGHT / PICKUP / CHECK-IN
    public string Title { get; set; }
    public DateTime When { get; set; }
    public string Subtitle { get; set; }
    public string Kind { get; set; }        // flight / hotel / transport
    public Guid? RefId { get; set; }
}

// ---------------- Itinerary ----------------
/// <summary>Everything on the guest's itinerary in one call. Empty list where
/// nothing is booked — never null.</summary>
public class ItinerarySummaryResponse
{
    public List<FlightLegResponse> Flights { get; set; } = new();
    public List<TransportationResponse> Transports { get; set; } = new();
    public List<AccommodationResponse> Accommodations { get; set; } = new();
    public List<GuestSessionResponse> Sessions { get; set; } = new();
}

// ---------------- Travel ----------------
/// <summary>One flight booking with its segments. A "return" booking holds two
/// legs (outbound + inbound); "inbound"/"outbound" hold one.</summary>
public class FlightBookingResponse
{
    public Guid Id { get; set; }
    public string FlightType { get; set; }  // inbound / outbound / return
    public string Class { get; set; }
    public string Status { get; set; }      // Confirmed / Pending
    public string Seat { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    /// <summary>Whole booking, first leg's departure to last leg's arrival.</summary>
    public string Duration { get; set; }
    public List<FlightLegResponse> Legs { get; set; } = new();
}

public class FlightLegResponse
{
    public Guid Id { get; set; }
    public string DepartureCode { get; set; }
    public string DepartureAirport { get; set; }
    public string ArrivalCode { get; set; }
    public string ArrivalAirport { get; set; }
    public DateTime? DateTime { get; set; }
    // Booking-level times from the Flights row (the leg's own times drive DateTime).
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public string FlightNumber { get; set; }
    public string Class { get; set; }
    public string Status { get; set; }      // Confirmed / Pending
    public string Duration { get; set; }
    public string Seat { get; set; }
}

public class AccommodationResponse
{
    public Guid Id { get; set; }
    public string HotelName { get; set; }
    public string HotelImageUrl { get; set; }
    public string Address { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public string RoomType { get; set; }
}

public class TransportationResponse
{
    public Guid Id { get; set; }
    public string FromAddress { get; set; }
    public DateTime? PickupTime { get; set; }
    public string ToAddress { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public string VehicleType { get; set; }
    public string TripStatus { get; set; }  // On Time
    /// <summary>The concrete car assigned, when dispatch has picked one. Null
    /// until then — VehicleType above is the category either way.</summary>
    public VehicleResponse Vehicle { get; set; }
    public DriverResponse Driver { get; set; }
}

public class VehicleResponse
{
    public Guid Id { get; set; }
    public string Model { get; set; }
    public string Number { get; set; }      // plate
    public string Type { get; set; }        // VehicleType category name
    public string ImageUrl { get; set; }
    public int? Capacity { get; set; }
}

/// <summary>Guest asking for a car. Lands as a Transport with status "new" and no
/// driver, until a driver accepts it from the driver app.</summary>
public class TransportRequest
{
    public Guid PickupLocationId { get; set; }
    public Guid DropoffLocationId { get; set; }
    /// <summary>Chosen vehicle (GET /v1/vehicles). Optional — dispatch can fill it later.</summary>
    public Guid? VehicleId { get; set; }
    public DateTime? PickupTime { get; set; }
    public DateTime? DropoffTime { get; set; }
}

public class DriverResponse
{
    public string Name { get; set; }
    public string Role { get; set; }
    public string Phone { get; set; }
}

public class ContactResponse
{
    public string Name { get; set; }
    public string Role { get; set; }
    public string Phone { get; set; }
}

// ---------------- Session detail (seating) ----------------
public class SessionDetailResponse : GuestSessionResponse
{
    public string Duration { get; set; }
    public string Status { get; set; }
    public SeatingResponse Seating { get; set; }
}

public class SeatingResponse
{
    public string Category { get; set; }    // VIP
    public string Block { get; set; }
    public string Row { get; set; }
    public string Seat { get; set; }
    public string Gate { get; set; }        // Enter via VIP Gate 3 · West Stand
}

// ---------------- Preferences ----------------
public class GuestPreferencesResponse
{
    public FlightPrefs Flight { get; set; } = new();
    public TransportPrefs Transport { get; set; } = new();
    public AccommodationPrefs Accommodation { get; set; } = new();
}
public class FlightPrefs { public string Seat { get; set; } public string Meal { get; set; } public string Notes { get; set; } }
public class TransportPrefs { public string VehicleType { get; set; } public string ChauffeurLanguage { get; set; } }
public class AccommodationPrefs { public string RoomType { get; set; } public string FloorPreference { get; set; } }

// ---------------- Profile ----------------
public class GuestProfileResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }
}

public record UpdateProfileRequest(string FirstName, string LastName, string Organization);
public record UpdateSettingsRequest(bool NotificationsEnabled, string Language, string Location);

// ---------------- Support ----------------
public class SupportMessageResponse
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Body { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string AttachmentUrl { get; set; }
    public string AttachmentType { get; set; }
    public string SenderName { get; set; } // populated for admin-sent messages

    // Sent-vs-received from the caller's own point of view. Only set by the
    // driver<->guest thread endpoint (ChatController), where both participants
    // read the same thread; FromGuest alone can't tell them apart. Always false
    // on the admin/guest support endpoints, which each only ever serve one side.
    public bool IsMine { get; set; }
}

// Plain class (not a positional record) so every field is independently
// optional on the wire — a record's positional ctor makes the first parameter
// awkward to omit. A message needs Body, an attachment, or both; never neither.
public class SendSupportMessageRequest
{
    public string Body { get; set; }
    public string AttachmentUrl { get; set; }
    public string AttachmentType { get; set; }
}

// ---------------- Notifications / devices ----------------
public class GuestNotificationResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
    public bool Read { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RedirectUrl { get; set; }
    public string Data { get; set; }
}
public record RegisterDeviceRequest(string Token, string Platform); // ios / android
