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
    public string Tier { get; set; }
    public int? ServiceLevelId { get; set; }
    public bool ServiceLevelRulesOverridden { get; set; }
    public string ServiceLevelOverrideReason { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public bool AccreditationRequired { get; set; }
    public string AllowedServicesJson { get; set; }
    public virtual Guest Guest { get; set; }
    public virtual Event Event { get; set; }
    public virtual Organization OrganizationRef { get; set; }
    public virtual ServiceLevel ServiceLevel { get; set; }
    public virtual ICollection<GuestSession> GuestSessions { get; set; } = new List<GuestSession>();
    public virtual ICollection<GuestServiceEntry> ServiceEntries { get; set; } = new List<GuestServiceEntry>();
}
