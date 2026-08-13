using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>
/// One person's participation in one event — the join between <see cref="Guest"/>
/// (the person, identified by email, reused across events) and <see cref="Event"/>.
/// </summary>
/// <remarks>
/// <para>
/// Everything that only makes sense "for this event" lives here rather than on
/// Guest: the service level they were graded at, their guest type, whether they
/// need an accreditation badge, and which built-in services they may self-request
/// from the VIP app. A person invited to a second event gets a second EventGuest
/// row against the SAME Guest row — no duplicate person record.
/// </para>
/// <para>
/// Every per-event child record hangs off this row's Id, not off Guest.Id:
/// GuestSessions, Invitations, Flights, Accommodations, Transports,
/// GuestServiceEntries, SeatAssigns and GuestDriverAssignments. That is what keeps
/// "which event is this flight for?" answerable — on Guest it would not be.
/// </para>
/// <para>
/// Unique on (GuestId, EventId): a person holds at most ONE participation per
/// event. Two service levels in the same event is deliberately not a case the
/// model allows; a person in two events is.
/// </para>
/// </remarks>
public class EventGuest : Entity
{
    public int GuestId { get; set; }
    public int EventId { get; set; }

    /// <summary>delegate / media / speaker … — per event, since the same person can
    /// attend one event as press and another as a delegate.</summary>
    public string GuestType { get; set; }

    /// <summary>Free-text fallback, kept in sync with OrganizationRef.Name — same
    /// mirroring convention this column had on Guest.</summary>
    public string Organization { get; set; }
    public int? OrganizationId { get; set; }

    /// <summary>The guest's grade for THIS event. Nullable: a participation may be
    /// created before a level is chosen.</summary>
    public int? ServiceLevelId { get; set; }

    /// <summary>Set when an authorised user pushed this assignment through despite a
    /// failing Service Level rule (capacity full / missing required fields). Kept
    /// for audit — diplomatic events need to show who waived what.</summary>
    public bool ServiceLevelRulesOverridden { get; set; }
    public string ServiceLevelOverrideReason { get; set; }

    /// <summary>Does this participation need an accreditation badge at all? The
    /// issue/revoke lifecycle itself lives on the Invitation row.</summary>
    public bool AccreditationRequired { get; set; }

    /// <summary>Which of the fixed Flight/Accommodation/Transport set this guest may
    /// self-request in the VIP app — see Core.Constants.GuestServiceType.</summary>
    public string AllowedServicesJson { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual Event Event { get; set; }
    public virtual Organization OrganizationRef { get; set; }
    public virtual ServiceLevel ServiceLevel { get; set; }

    public virtual ICollection<GuestSession> GuestSessions { get; set; } = new List<GuestSession>();
    public virtual ICollection<GuestServiceEntry> ServiceEntries { get; set; } = new List<GuestServiceEntry>();
}
