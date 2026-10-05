using System;
using System.Collections.Generic;

namespace Core.ViewModel.Event;

public class EventResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; }
    public string AppKey { get; set; }
    /// <summary>fixed | flexible — whether this event runs the Service Level flow.</summary>
    public string GuestModel { get; set; }
    public string ImageUrl { get; set; }
    public string AttachmentUrl { get; set; }

    // ── Mission fields ───────────────────────────────────────────────────
    public Guid? DestinationId { get; set; }
    public string DestinationAddress { get; set; }
    public int? DelegationCap { get; set; }
    public string DestinationTier { get; set; }
    public string CostCenter { get; set; }
    public string FundingModel { get; set; }
    public Guid? HostOrganizationId { get; set; }
    /// <summary>The organisation's name, resolved at save time.</summary>
    public string HostName { get; set; }
    public string HostEmail { get; set; }
    /// <summary>The invitation this mission was created from, if any.</summary>
    public Guid? HostInvitationId { get; set; }
    /// <summary>True once EndDate has passed. Derived, never stored — a completed
    /// mission takes no new delegates or bookings, but still accepts reports.</summary>
    public bool IsCompleted { get; set; }

    public List<SessionResponse> Sessions { get; set; } = new();
}

public class SessionResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Title { get; set; }
    public DateOnly? Date { get; set; }
    public string Time { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public string Room { get; set; }
    public string Speaker { get; set; }
    public int Capacity { get; set; }
    public string ImageUrl { get; set; }
}
