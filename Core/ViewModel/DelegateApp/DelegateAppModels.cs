using System;
using System.Collections.Generic;

namespace Core.ViewModel.DelegateApp;

// ============================================================================
// VIP Guest App — view models (mobile-facing).
// One file on purpose: these are thin DTOs for a single controller. Split only
// if a group starts growing its own logic. ponytail: flat until it hurts.
// ============================================================================

// ---------------- Auth ----------------
public record RequestOtpRequest(string Email);

// Device fields are optional, so an older app build sending only (Email, Code)
// still works — when DeviceToken is present verify-otp registers the push
// token itself, saving a second round-trip to POST /notifications/devices.
public record VerifyOtpRequest(
    string Email,
    string Code,
    string DeviceToken = null,
    string Platform = null,          // ios | android | web
    string DeviceIdentifier = null,
    string DeviceModel = null);
public record RefreshTokenRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

public class GuestAuthResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public GuestProfileResponse Guest { get; set; }

    // The guest's linked User's Device row. verify-otp upserts it when the
    // request carries a DeviceToken, otherwise echoes the most recently
    // active registration.
    public string FcmToken { get; set; }
    public string DeviceId { get; set; }
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
    /// <summary>The mission's own status (planning / active / completed / ...).</summary>
    public string Status { get; set; }

    // ── Mission card (home hero / switch-mission sheet) ──
    /// <summary>"SF-2027" — initials of the title plus its year. Derived, see MissionCode.</summary>
    public string Code { get; set; }
    public string VenueName { get; set; }
    public string Destination { get; set; }
    /// <summary>What the DELEGATE sees: nominated / upcoming / live / completed /
    /// cancelled. See DelegateMissionStatuses.</summary>
    public string MissionStatus { get; set; }
    /// <summary>"Day 4 of 6" while live; null otherwise.</summary>
    public int? CurrentDay { get; set; }
    public int? TotalDays { get; set; }
    /// <summary>Their role on this mission (Head of Delegation, Member, ...).</summary>
    public string MissionRole { get; set; }
    public string Subgroup { get; set; }
    public string HrVerificationStatus { get; set; }
}

/// <summary>Mission status as the delegate app shows it — one per Figma state.</summary>
public static class DelegateMissionStatuses
{
    public const string Nominated = "nominated";
    public const string Upcoming  = "upcoming";
    public const string Live      = "live";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

// ---------------- Mission home ----------------
public class MissionHomeResponse
{
    public GuestEventResponse Mission { get; set; }
    /// <summary>The coordinator who nominated them — "Invite from …" on the
    /// activation card and the one-tap emergency contact. Null if unknown.</summary>
    public ContactResponse Coordinator { get; set; }
    /// <summary>The next upcoming flight / check-in / pickup. Null when nothing is ahead.</summary>
    public AgendaCardResponse NextUp { get; set; }
    public VisaTrackerResponse Visa { get; set; }
    /// <summary>passport / visa / flight / accommodation / transport, each
    /// met / waived / missing — the same derivation the portal's readiness board uses.</summary>
    public List<ReadinessCheckResponse> Checklist { get; set; } = new();
    public bool TravelReady { get; set; }
    public JournalSummaryResponse Journal { get; set; }
    /// <summary>Core.Constants.PostMissionReportStatuses.</summary>
    public string ReportStatus { get; set; }
    public int UnreadNotifications { get; set; }

    public AllowanceResponse Allowance { get; set; }
    /// <summary>Nominated missions: nominated → hr_verification → host_confirmation
    /// → invite_travel. Empty otherwise.</summary>
    public List<StepResponse> NominationSteps { get; set; } = new();
    /// <summary>Cancelled missions only.</summary>
    public CancellationResponse Cancellation { get; set; }
    /// <summary>Completed missions only.</summary>
    public MissionSummaryResponse Summary { get; set; }
}

public class StepResponse
{
    public string Key { get; set; }
    public bool Done { get; set; }
    public bool Current { get; set; }
    public DateTime? Date { get; set; }
    /// <summary>Who / what, from the record — e.g. the nominator's name and role.</summary>
    public string Detail { get; set; }
}

public class AllowanceResponse
{
    /// <summary>Core.Constants.PerDiemStatuses; null = nothing recorded yet.</summary>
    public string Status { get; set; }
    public decimal? Amount { get; set; }
    public string Currency { get; set; }
    public decimal? SettlementAmount { get; set; }
    public DateTime? SettlementPaidOn { get; set; }
    /// <summary>Event.FundingModel — org_paid / hosted / mixed.</summary>
    public string FundingModel { get; set; }
}

public class CancellationResponse
{
    public string Reason { get; set; }
    public DateTime? CancelledAt { get; set; }
    /// <summary>What was booked for this mission and where it stands now.</summary>
    public List<UndoneItemResponse> Bookings { get; set; } = new();
}

public class UndoneItemResponse
{
    public string Kind { get; set; }    // flight / stay / transport / visa
    public string Title { get; set; }   // "LHR → MRS", hotel name, ...
    public string Status { get; set; }  // the booking's own status
}

public class MissionSummaryResponse
{
    public int? DaysAttended { get; set; }
    /// <summary>The delegate's "key outcomes" from their report, one per line.</summary>
    public List<string> Results { get; set; } = new();
    public DateTime? ReportSubmittedOn { get; set; }
    public DateTime? ReportApprovedOn { get; set; }
    public string ReportApprovedBy { get; set; }
    public int JournalEntries { get; set; }
}

public class ReadinessCheckResponse
{
    public string Key { get; set; }
    public string Status { get; set; }   // met / waived / missing
    public string Detail { get; set; }
}

/// <summary>Visa stepper: required → applied → issued. Steps carry the date
/// they were reached; null = not reached yet.</summary>
public class VisaTrackerResponse
{
    public bool Required { get; set; }
    /// <summary>EventGuest.VisaStatus — pending / active / expiring-soon.</summary>
    public string Status { get; set; }
    /// <summary>not_required / not_started / applied / issued.</summary>
    public string Stage { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? IssuedAt { get; set; }

    // From the visa service entry, read against the service's form schema — so
    // a field the admin adds in the portal (e.g. "Fee paid on") shows up here too.
    /// <summary>Every date field except expiry, in form order: done once it has a value.</summary>
    public List<VisaStepResponse> Steps { get; set; } = new();
    /// <summary>Non-date, non-file fields: Visa type, Reference no., Notes ...</summary>
    public List<LabelValueResponse> Details { get; set; } = new();
    public DateOnly? ExpiresOn { get; set; }
    public string DocumentUrl { get; set; }
    public string Country { get; set; }
    /// <summary>The staff member who recorded the entry (name · role).</summary>
    public string HandledBy { get; set; }
}

public class VisaStepResponse
{
    public string Key { get; set; }
    public string Label { get; set; }
    public DateOnly? Date { get; set; }
    public bool Done { get; set; }
}

public class LabelValueResponse
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string Value { get; set; }
}

// ---------------- Accreditation ----------------
public class AccreditationResponse
{
    public bool Required { get; set; }
    /// <summary>Invitation.AccreditationStatus as stored; null when no invitation row.</summary>
    public string Status { get; set; }
    public string DelegateName { get; set; }
    public string PhotoUrl { get; set; }
    public string Organization { get; set; }
    public string MissionRole { get; set; }
    public string Tier { get; set; }
    /// <summary>Validity = the mission's dates.</summary>
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    /// <summary>The QR stays locked until the visa is in hand (or not needed).</summary>
    public bool QrLocked { get; set; }
    public string LockReason { get; set; }
    /// <summary>Null while locked. The badge number, or the participation's public
    /// id when no badge number has been issued yet.</summary>
    public string QrPayload { get; set; }

    public string Number { get; set; }
    public string Category { get; set; }
    public string CategoryLabel { get; set; }
    public List<AccreditationZoneResponse> Zones { get; set; } = new();
    /// <summary>A badge number exists and the QR is unlocked.</summary>
    public bool Issued { get; set; }
}

public class AccreditationZoneResponse
{
    public int Number { get; set; }
    public string Label { get; set; }
}

// ---------------- Stays ----------------
public class StaysResponse
{
    /// <summary>Event.FundingModel of the mission — "hosted" / "org_paid" / "mixed".</summary>
    public string FundingModel { get; set; }
    public List<AccommodationResponse> Upcoming { get; set; } = new();
    public List<AccommodationResponse> History { get; set; } = new();
}

// ---------------- Help requests (→ Incidents, RaisedVia = app) ----------------
public class HelpRequest
{
    public Guid EventId { get; set; }
    /// <summary>medical / late / lost / transport / other.</summary>
    public string Category { get; set; }
    public string Description { get; set; }
}

public class HelpRequestResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Category { get; set; }
    public string Severity { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

// ---------------- Mission journal ----------------
public class JournalSummaryResponse
{
    public int Total { get; set; }
    public int InReport { get; set; }
    public DateTime? LastAddedAt { get; set; }
    /// <summary>False once the mission is closed — the app hides every write action.</summary>
    public bool Editable { get; set; }
}

public class JournalResponse
{
    public JournalSummaryResponse Summary { get; set; }
    public List<JournalDayResponse> Days { get; set; } = new();
}

/// <summary>A mission day that has a programme item or at least one entry.</summary>
public class JournalDayResponse
{
    public DateOnly Date { get; set; }
    /// <summary>1-based day of the mission; null if the mission has no start date.</summary>
    public int? DayNumber { get; set; }
    public List<string> Programme { get; set; } = new();
    public List<JournalEntryResponse> Entries { get; set; } = new();
}

public class JournalEntryResponse
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public Guid? SessionId { get; set; }
    public string Type { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public string FileUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public bool IncludedInReport { get; set; }
    public string Visibility { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Multipart form: a note needs Title or Body; any other type needs File.</summary>
public class CreateJournalEntryRequest
{
    public DateOnly Date { get; set; }
    public string Type { get; set; }
    public Guid? SessionId { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public bool IncludedInReport { get; set; }
    /// <summary>private / shared (default).</summary>
    public string Visibility { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile File { get; set; }
}

/// <summary>Only the fields sent are changed.</summary>
public record UpdateJournalEntryRequest(bool? IncludedInReport, string Visibility = null);

// ---------------- Post-mission report ----------------
public class DelegateReportResponse
{
    /// <summary>Core.Constants.PostMissionReportStatuses.</summary>
    public string Status { get; set; }
    public string Narrative { get; set; }
    public DateTime? SubmittedOn { get; set; }
    /// <summary>True only after the mission has ended and until the report is approved.</summary>
    public bool Editable { get; set; }
    /// <summary>Why it can't be written, when Editable is false.</summary>
    public string LockedReason { get; set; }
    public string KeyOutcomes { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public string ApprovedBy { get; set; }
    /// <summary>Journal entries ticked for the report.</summary>
    public List<JournalEntryResponse> Evidence { get; set; } = new();
    /// <summary>Filled from the trip records — the delegate doesn't type these.</summary>
    public ReportAutoFillResponse AutoFill { get; set; }
}

public class ReportAutoFillResponse
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>Programme items they journalled against.</summary>
    public List<string> ProgrammeAttended { get; set; } = new();
    public List<string> Hotels { get; set; } = new();
    public List<string> Vehicles { get; set; } = new();
    public int JournalTotal { get; set; }
    public int InReport { get; set; }
}

public record SaveDelegateReportRequest(string Narrative, bool Submit, string KeyOutcomes = null);

// ---------------- Booking change requests ----------------
public class ChangeRequestCreate
{
    public Guid EventId { get; set; }
    /// <summary>flight / stay.</summary>
    public string BookingKind { get; set; }
    /// <summary>The flight booking id or the stay id.</summary>
    public Guid BookingId { get; set; }
    /// <summary>Flight: date_change / route_change / seat_change / extra_baggage /
    /// cancel_segment. Stay: extra_night / room_change / change_hotel /
    /// early_check_in / late_check_out.</summary>
    public string ChangeType { get; set; }
    public string Notes { get; set; }
    /// <summary>route_change only (airport ids from /lookup/airports).</summary>
    public Guid? FromAirportId { get; set; }
    public Guid? ToAirportId { get; set; }
    /// <summary>change_hotel only — one of GET missions/{id}/hotels.</summary>
    public Guid? HotelId { get; set; }
}

public class ChangeRequestResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string BookingKind { get; set; }
    public Guid? BookingId { get; set; }
    /// <summary>"LHR → DOH · QR 016" / "Al Sadaf Hotel · 29 Dec – 31 Jan".</summary>
    public string BookingLabel { get; set; }
    public string ChangeType { get; set; }
    public string Notes { get; set; }
    public string FromAirport { get; set; }
    public string ToAirport { get; set; }
    public string Hotel { get; set; }
    /// <summary>requested ("With coordinator") / done / declined.</summary>
    public string Status { get; set; }
    public string ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Portal list only.</summary>
    public string DelegateName { get; set; }
}

public class ResolveChangeRequest
{
    /// <summary>done / declined.</summary>
    public string Status { get; set; }
    public string Note { get; set; }
}

// ---------------- Places (transport location picker) ----------------
public class PlacesResponse
{
    /// <summary>Their hotels, the mission venue(s) and airports on their flights.</summary>
    public List<PlaceResponse> Suggested { get; set; } = new();
    /// <summary>Other places their rides have used.</summary>
    public List<PlaceResponse> Recent { get; set; } = new();
}

public class PlaceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    /// <summary>Locations.Type — hotel / venue / airport / custom / ...</summary>
    public string Type { get; set; }
    public string Latitude { get; set; }
    public string Longitude { get; set; }
}

// ---------------- Programme (itinerary) ----------------
public class ProgrammeItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public DateOnly? Date { get; set; }
    /// <summary>"HH:mm"; null = "Time TBC".</summary>
    public string Time { get; set; }
    public string VenueName { get; set; }
    public string Room { get; set; }
    public int? DayNumber { get; set; }
    public int JournalCount { get; set; }
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

    /// <summary>May this guest request a car themselves (POST transport-requests)?
    /// Independent of Transports: the admin can allow requests without booking
    /// anything, so the app shows the request screen on an empty transport list.
    /// Set from Guest.AllowedServicesJson — see Core.Constants.GuestServices.</summary>
    public bool TransportAllowed { get; set; }

    /// <summary>The mission's whole programme (every session), only when eventId
    /// is given. Separate from Sessions, which are only the ones picked for them.</summary>
    public List<ProgrammeItemResponse> Programme { get; set; } = new();
    /// <summary>provisional while any programme item has no time yet, else confirmed.
    /// Null without eventId.</summary>
    public string ProgrammeStatus { get; set; }
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
    /// <summary>Ticket / boarding pass, when the organiser uploaded one.</summary>
    public string ImageUrl { get; set; }
    public string Airline { get; set; }
    public string Pnr { get; set; }
    public string ETicketNumber { get; set; }
    /// <summary>The staff member who entered the booking.</summary>
    public string BookedBy { get; set; }
    public DateTime? BookedOn { get; set; }
    /// <summary>Event.FundingModel — who pays.</summary>
    public string FundingModel { get; set; }
    /// <summary>upcoming / completed (last leg landed) — drives Upcoming vs History.</summary>
    public string TravelState { get; set; }
    public Guid? EventId { get; set; }
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
    public string BaggageAllowance { get; set; }
    public DateTime? BoardingTime { get; set; }
    public string DepartureCity { get; set; }
    public string ArrivalCity { get; set; }
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

    /// <summary>This booking's own image (voucher, room photo) — separate from
    /// <see cref="HotelImageUrl"/>, which is the hotel's picture.</summary>
    public string ImageUrl { get; set; }

    public string RoomNumber { get; set; }
    public string ConfirmationNumber { get; set; }
    public string BoardBasis { get; set; }
    /// <summary>"HH:mm".</summary>
    public string CheckInTime { get; set; }
    public string CheckOutTime { get; set; }
    public int? Nights { get; set; }
    /// <summary>upcoming / checked_in / completed — derived from the dates.</summary>
    public string StayState { get; set; }
    public Guid? EventId { get; set; }
}

public class TransportationResponse
{
    public Guid Id { get; set; }
    public string FromAddress { get; set; }
    public DateTime? PickupTime { get; set; }
    public string ToAddress { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    /// <summary>Pickup / drop-off coordinates so the app can pin the ride on a
    /// map. Strings because that is how Locations stores them; null when the
    /// point has no location row or was never geocoded.</summary>
    public string FromLatitude { get; set; }
    public string FromLongitude { get; set; }
    public string ToLatitude { get; set; }
    public string ToLongitude { get; set; }
    public string VehicleType { get; set; }
    public string TripStatus { get; set; }  // On Time
    public string Notes { get; set; }
    /// <summary>The concrete car assigned, when dispatch has picked one. Null
    /// until then — VehicleType above is the category either way.</summary>
    public VehicleResponse Vehicle { get; set; }
    public DriverResponse Driver { get; set; }
}

/// <summary>Everything that isn't today: rides still to come, and rides already
/// done or cancelled. Empty lists, never null.</summary>
public class TransportBookingsResponse
{
    public List<TransportationResponse> Upcoming { get; set; } = new();
    public List<TransportationResponse> History { get; set; } = new();
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
    /// <summary>Guid.Empty when <see cref="Pickup"/> is sent instead.</summary>
    public Guid PickupLocationId { get; set; }
    public Guid DropoffLocationId { get; set; }
    public Guid EventId { get; set; }
    /// <summary>Null = now. A scheduled time must be in the future.</summary>
    public DateTime? PickupTime { get; set; }
    /// <summary>For the driver, e.g. "arrivals exit 2, two bags".</summary>
    public string Notes { get; set; }

    /// <summary>Instead of a location id: "use my current location" or a typed
    /// address. Saved as a Locations row of type "custom".</summary>
    public PlaceInput Pickup { get; set; }
    public PlaceInput Dropoff { get; set; }
}

public class PlaceInput
{
    public string Address { get; set; }
    public string Latitude { get; set; }
    public string Longitude { get; set; }
}

/// <summary>Guest cancelling their own transport request.</summary>
public class CancelTransportRequest
{
    public Guid Id { get; set; }
}

public class DriverResponse
{
    public string Name { get; set; }
    public string Role { get; set; }
    public string Phone { get; set; }
    public Guid DriverUserId { get; set; }
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

    /// <summary>The guest's photo — the profile screen's avatar and the home
    /// screen's header. Stored bare in the DB; BlobSasMiddleware attaches a
    /// short-lived read SAS on the way out, so the app can load it directly.
    /// Null when no photo was uploaded — fall back to initials.</summary>
    public string PhotoUrl { get; set; }

    // Person-level record from DMS (read-only in the app).
    public string JobTitle { get; set; }
    public string Department { get; set; }
    public string Nationality { get; set; }
    /// <summary>Last 4 characters only, e.g. "•••• 4821".</summary>
    public string PassportNumberMasked { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public string Phone { get; set; }
    public bool NotificationsEnabled { get; set; }
    public string Language { get; set; }
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

    // Sent-vs-received from the CALLER's own point of view — the same message row
    // is IsMine to whoever wrote it and not to the other side, so every endpoint
    // sets it for the audience it serves: the guest's support thread (their own
    // messages), the admin inbox (anything not from the guest), and the
    // driver<->guest thread (by actual SenderUserId, since both participants read
    // one thread and FromGuest alone can't tell them apart).
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

// Update fields on an already-registered Device row. Token identifies the
// row (must belong to the caller); every other field is optional — only
// non-null ones are applied. Use POST devices instead if the token itself
// changed (that's an upsert, not an update).
public class UpdateDeviceRequest
{

    public Guid UserId { get; set; }
    public string Token { get; set; }
    public string DeviceIdentifier { get; set; }
    public string Platform { get; set; }
    public string DeviceModel { get; set; }
    public string OsVersion { get; set; }
    public string AppVersion { get; set; }
    public bool? NotificationsEnabled { get; set; }
}

public class DeviceResponse
{
    public Guid Id { get; set; }
    public string Token { get; set; }
    public string Platform { get; set; }
    public string DeviceIdentifier { get; set; }
    public string DeviceModel { get; set; }
    public string OsVersion { get; set; }
    public string AppVersion { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime? TokenUpdatedAt { get; set; }
}
