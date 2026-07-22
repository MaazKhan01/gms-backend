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
    }

    public class DashboardSessionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public DateOnly? Date { get; set; }
        public string Time { get; set; }
        public string? Room { get; set; }
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
        public string? Organization { get; set; }
        public string? Tier { get; set; }
        public string? InvitationStatus { get; set; }
        public DateOnly? ArrivalDate { get; set; }
    }
}
