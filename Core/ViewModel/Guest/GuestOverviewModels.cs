using System;
using System.Collections.Generic;
using Core.ViewModel.Common;
using Core.ViewModel.ServiceCatalog;

namespace Core.ViewModel.Guest;

// ── List: one lightweight row per guest, system-wide (all events) ──────────
// Deliberately flatter than GuestResponse — this feeds a table that may be
// paging over thousands of rows, so no per-row nested collections here.
// Counts/flags are cheap correlated-subquery projections; full nested detail
// (sessions/flights/etc.) is a separate on-demand call, see
// GuestOverviewDetailResponse below.
public class GuestOverviewPagedRequest : PagedRequest
{
    public Guid? EventId { get; set; }
    public Guid? ServiceLevelId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? NationalityId { get; set; }
    public Guid? SessionId { get; set; }
    public string GuestType { get; set; }
    public string Tier { get; set; }

    /// <summary>Comma-separated, e.g. "sent,opened,accepted" — same convention
    /// as GuestPagedRequest.InvitationStatuses.</summary>
    public string InvitationStatus { get; set; }

    /// <summary>"not_required" | "pending" | "issued".</summary>
    public string AccreditationStatus { get; set; }

    public bool? HasFlight { get; set; }
    public bool? HasAccommodation { get; set; }
    public bool? HasTransport { get; set; }

    /// <summary>Guests with at least one non-completed dynamic GuestServiceEntry.</summary>
    public bool? HasPendingServices { get; set; }

    public DateOnly? ArrivalFrom { get; set; }
    public DateOnly? ArrivalTo { get; set; }
    public DateOnly? DepartureFrom { get; set; }
    public DateOnly? DepartureTo { get; set; }
}

public class GuestOverviewRow
{
    /// <summary>The PublicId of this person's most recent Guest row (their most
    /// recent event participation) — used to resolve the full cross-event detail
    /// via GET /v1/guest-overview/{id}. Not a stable "person id": there isn't
    /// one, see GroupByEmail below.</summary>
    public Guid Id { get; set; }

    /// <summary>The person's own PublicId (Guests.PublicId) — stable across every
    /// event they attend, and the real identity this row is grouped on.</summary>
    public Guid PersonId { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; }
    public string PhotoUrl { get; set; }
    public string GuestType { get; set; }

    /// <summary>Most recent event participation — shown as the row's "Event"
    /// column. See <see cref="EventsCount"/>/<see cref="EventTitles"/> for the
    /// full spread when a person is in more than one.</summary>
    public Guid EventId { get; set; }
    public string EventTitle { get; set; }

    /// <summary>How many distinct events this person (by email) participates
    /// in, and each one's title — a guest can legitimately be a brand-new Guest
    /// row per event (no shared person identity in the data model), so the list
    /// groups by email to show one row per person rather than one per booking.</summary>
    public int EventsCount { get; set; }
    public List<string> EventTitles { get; set; } = new();

    public string Organization { get; set; }
    public string NationalityName { get; set; }
    public string NationalityFlag { get; set; }

    public string Tier { get; set; }
    public Guid? ServiceLevelId { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelColor { get; set; }

    public string InvitationStatus { get; set; }
    public string AccreditationStatus { get; set; }

    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }

    // Enough for the table to show "3 sessions" / "2 pending" and a
    // flight/hotel/car glyph without loading each guest's full detail.
    public int SessionsCount { get; set; }
    public int ServicesCount { get; set; }
    public int PendingServicesCount { get; set; }
    public int SeatsCount { get; set; }
    public bool HasFlight { get; set; }
    public bool HasAccommodation { get; set; }
    public bool HasTransport { get; set; }

    public DateTime CreatedAt { get; set; }
}

// ── Detail: fetched only when a row expands ─────────────────────────────────
// One section per data source, matching the accordion's Events / Sessions /
// Flights / Seatings / Accommodations / Transport / Other services layout.
//
// Scoped to a PERSON (every Guest row sharing the anchor guest's email), not
// to the single Guest row `id` names — the data model has no shared identity
// across events, so this is where that gets stitched back together. Events is
// therefore a list (one block per event participation); every other section
// flattens across all of that person's events, each item tagged with which
// event it came from.
public class GuestOverviewDetailResponse
{
    /// <summary>The anchor guest row's id — kept for reference, not a stable
    /// person id (there isn't one; see Email).</summary>
    public Guid Id { get; set; }
    public string Email { get; set; }
    public List<GuestOverviewEventBlock> Events { get; set; } = new();
    public List<GuestOverviewSessionRow> Sessions { get; set; } = new();
    public List<GuestOverviewFlightRow> Flights { get; set; } = new();
    public List<GuestOverviewAccommodationRow> Accommodations { get; set; } = new();
    public List<GuestOverviewTransportRow> Transport { get; set; } = new();
    public List<GuestOverviewSeatRow> Seatings { get; set; } = new();

    /// <summary>Every service on each event's level that isn't Flight/Accommodation/
    /// Transport — those three already have their own sections above.</summary>
    public List<GuestOverviewOtherServiceRow> OtherServices { get; set; } = new();
}

public class GuestOverviewEventBlock
{
    /// <summary>This specific event participation's own Guest row id — the one
    /// to use if some future action needs to edit that particular booking.</summary>
    public Guid GuestId { get; set; }
    public Guid EventId { get; set; }
    public string EventTitle { get; set; }
    public string EventType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string VenueName { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelColor { get; set; }
    public string InvitationStatus { get; set; }
    public string AccreditationStatus { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
}

public class GuestOverviewSessionRow
{
    public string EventTitle { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; }
    public DateOnly? Date { get; set; }
    public string Time { get; set; }
    public string Room { get; set; }
    public string Speaker { get; set; }
    public string Status { get; set; }
}

public class GuestOverviewFlightRow
{
    public string EventTitle { get; set; }
    public Guid Id { get; set; }
    public string FlightType { get; set; }
    public string Status { get; set; }
    public string FlightClass { get; set; }
    public string Seat { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public string ImageUrl { get; set; }
    public List<GuestOverviewFlightLegRow> Legs { get; set; } = new();
}

public class GuestOverviewFlightLegRow
{
    public string FlightNumber { get; set; }
    public string DepartureCode { get; set; }
    public string DepartureCity { get; set; }
    public string ArrivalCode { get; set; }
    public string ArrivalCity { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string FlightClass { get; set; }
    public string Seat { get; set; }
}

public class GuestOverviewAccommodationRow
{
    public string EventTitle { get; set; }
    public Guid Id { get; set; }
    public string Hotel { get; set; }
    public string HotelImageUrl { get; set; }
    public string RoomType { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
    public string ImageUrl { get; set; }
}

public class GuestOverviewTransportRow
{
    public string EventTitle { get; set; }
    public Guid Id { get; set; }
    public string TripStatus { get; set; }
    public string Vehicle { get; set; }
    public string DriverName { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }
    public DateTime? PickupTime { get; set; }
    public DateTime? DropoffTime { get; set; }
}

public class GuestOverviewSeatRow
{
    public string EventTitle { get; set; }
    public string SessionTitle { get; set; }
    public string SeatCode { get; set; }
}

// Flattened GuestServiceSlotResponse + which event it came from — a person can
// hold this same dynamic service independently on more than one event's level.
public class GuestOverviewOtherServiceRow
{
    public string EventTitle { get; set; }
    public Guid ServiceId { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Icon { get; set; }
    public string Status { get; set; }
    public bool IsUnlocked { get; set; }
    public string LockedReason { get; set; }
    public List<GuestServiceEntryResponse> Entries { get; set; } = new();
}
