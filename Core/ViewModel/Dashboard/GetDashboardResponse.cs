using System;
using System.Collections.Generic;

namespace Core.ViewModel.Dashboard
{
    public class GetDashboardResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Venue { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public List<DashboardSessionDto> Sessions { get; set; } = new();
        public DashboardFunnelDto FunnelData { get; set; }
        public List<DashboardMeetingDto> Meetings { get; set; } = new();
        public List<DashboardGuestDto> RecentGuests { get; set; } = new();

        // ── Analytics ──────────────────────────────────────────────────────
        // Everything below is derived from the same guest/invitation/travel
        // rows already loaded for the funnel, so the extra detail costs no
        // additional round trips.
        public DashboardRsvpDto Rsvp { get; set; } = new();
        public DashboardAccreditationDto Accreditation { get; set; } = new();
        public DashboardTravelDto Travel { get; set; } = new();
        public DashboardSeatingDto Seating { get; set; } = new();

        /// <summary>Guests per service level, in the level's configured sort order.</summary>
        public List<DashboardBreakdownDto> ServiceLevels { get; set; } = new();
        public List<DashboardBreakdownDto> Nationalities { get; set; } = new();
        public List<DashboardBreakdownDto> Organizations { get; set; } = new();

        /// <summary>Arrival/departure counts per day, for the movements chart.</summary>
        public List<DashboardDayCountDto> Movements { get; set; } = new();

        /// <summary>The mission read as one sequence, phase by phase.</summary>
        public MissionJourneyDto Journey { get; set; } = new();
    }

    public class DashboardRsvpDto
    {
        public int Accepted { get; set; }
        public int Declined { get; set; }
        /// <summary>Invited but no answer yet — sent + opened.</summary>
        public int Awaiting { get; set; }
        /// <summary>No invitation has gone out at all.</summary>
        public int NotSent { get; set; }
        /// <summary>Accepted as a percentage of everyone actually invited (0 when none are).</summary>
        public int ResponseRate { get; set; }
    }

    public class DashboardAccreditationDto
    {
        public int Issued { get; set; }
        public int Pending { get; set; }
        public int Revoked { get; set; }
        /// <summary>Guests whose profile says accreditation isn't needed.</summary>
        public int NotRequired { get; set; }
    }

    public class DashboardTravelDto
    {
        public int FlightsBooked { get; set; }
        public int AccommodationBooked { get; set; }
        public int TransportBooked { get; set; }
        /// <summary>Distinct guests with at least one of the three.</summary>
        public int GuestsWithTravel { get; set; }
    }

    public class DashboardSeatingDto
    {
        public int Assigned { get; set; }
        public int Unassigned { get; set; }
    }

    /// <summary>A labelled slice of the guest list — service level, nationality, organization.</summary>
    public class DashboardBreakdownDto
    {
        public string Label { get; set; }
        public string LabelAr { get; set; }
        /// <summary>Only service levels carry one; null elsewhere.</summary>
        public string Color { get; set; }
        public int Count { get; set; }
    }

    public class DashboardDayCountDto
    {
        public DateOnly Date { get; set; }
        public int Arrivals { get; set; }
        public int Departures { get; set; }
    }

    public class DashboardSessionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public DateOnly? Date { get; set; }
        public string Time { get; set; }
        public string? Room { get; set; }
        public string? ImageUrl { get; set; }

        /// <summary>Guests checked into this session (GuestSession rows), scoped
        /// to this event's guest list.</summary>
        public int GuestCount { get; set; }
    }

    // All counts are scoped to the event's own guest list.
    // ConfirmedGuest: InvitationStatus == accepted.
    // AwaitingGuest: InvitationStatus == sent or opened (invited, no response yet —
    //   not_sent isn't counted since nothing has actually gone out to await a reply on).
    // TravelBooked: has a flight number, hotel, or assigned seat (no dedicated status field exists).
    // AccreditationIssued: AccreditationStatus == issued.
    public class DashboardFunnelDto
    {
        public int TotalGuests { get; set; }
        public int ConfirmedGuest { get; set; }
        public int AwaitingGuest { get; set; }
        public int TravelBooked { get; set; }
        public int AccreditationIssued { get; set; }
    }

    public class DashboardMeetingDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? Location { get; set; }
    }

    public class DashboardGuestDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Organization { get; set; }
        // Guest.Tier is a legacy mirror string — this is the real per-event grade
        // (Core/Constants ServiceLevel), null when the guest has none assigned.
        public string? ServiceLevelName { get; set; }
        public string? ServiceLevelColor { get; set; }
        public string? InvitationStatus { get; set; }
        public DateOnly? ArrivalDate { get; set; }
    }
}
