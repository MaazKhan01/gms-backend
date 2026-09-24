using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

public class EventGuest : Entity
{
    public int GuestId { get; set; }
    public int EventId { get; set; }
    public string GuestType { get; set; }
    public string Organization { get; set; }
    public int? OrganizationId { get; set; }
    public int? ServiceLevelId { get; set; }
    public bool ServiceLevelRulesOverridden { get; set; }
    public string ServiceLevelOverrideReason { get; set; }
    public bool AccreditationRequired { get; set; }
    public string AllowedServicesJson { get; set; }
    public string VisaStatus { get; set; }
    public string InsuranceStatus { get; set; }
    public bool VisaRequired { get; set; } = true;

    /// <summary>
    /// What this person is on THIS mission — a Role row flagged IsDelegateRole
    /// (Head of Delegation, Member, Support Staff, ...). Admin-created, so the
    /// delegate-type list is managed rather than compiled in.
    ///
    /// Deliberately per-participation, not on the User: someone can lead one
    /// delegation and travel as an ordinary member of the next. Separate from
    /// <c>User.RoleId</c>, which is the person's global PORTAL identity and is
    /// what the access token is minted from — granting a mission role here does
    /// not by itself let anyone into the portal.
    /// </summary>
    public int? MissionRoleId { get; set; }

    public string Subgroup { get; set; }

    public DateTime? NominatedOn { get; set; }
    public int? NominatedBy { get; set; }

    // HR verification

    /// <summary>Core.Constants.HrVerificationStatuses — pending / verified /
    /// rejected. A nomination lands as pending; HR either confirms the passport,
    /// grade, visa and insurance records or sends it back with a note.</summary>
    public string HrVerificationStatus { get; set; }

    public DateTime? HrVerifiedOn { get; set; }
    public int? HrVerifiedBy { get; set; }
    public string HrVerificationNote { get; set; }

    public virtual Guest Guest { get; set; }
    public virtual Event Event { get; set; }
    public virtual Organization OrganizationRef { get; set; }
    public virtual ServiceLevel ServiceLevel { get; set; }
    public virtual Role MissionRole { get; set; }
    public virtual User NominatedByUser { get; set; }
    public virtual User HrVerifiedByUser { get; set; }
    public virtual ICollection<ReadinessWaiver> ReadinessWaivers { get; set; } = new List<ReadinessWaiver>();
    public virtual PostMissionReport PostMissionReport { get; set; }
    public virtual ICollection<GuestSession> GuestSessions { get; set; } = new List<GuestSession>();
    public virtual ICollection<GuestServiceEntry> ServiceEntries { get; set; } = new List<GuestServiceEntry>();
}
