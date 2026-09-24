using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Writable after the mission is marked completed — the completion guard stops
/// new delegates and bookings, not the reporting that by definition comes after.
/// </summary>
public class PostMissionReport : Entity
{
    public int EventGuestId { get; set; }
    public string Status { get; set; }

    /// <summary>The delegate's own account. Trip facts (dates, hotel, transport)
    /// are read from the booking records at render time rather than copied here,
    /// so a late correction to a booking is reflected automatically.</summary>
    public string Narrative { get; set; }

    public DateTime? SubmittedOn { get; set; }

    /// <summary>How many reminders have gone out. Stored rather than derived
    /// because the combined-report screen ranks by it.</summary>
    public int NudgeCount { get; set; }

    public DateTime? LastNudgeOn { get; set; }

    public virtual EventGuest EventGuest { get; set; }
}
