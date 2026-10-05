using System;

namespace DomainPersistence.Entities;

/// <summary>
/// <see cref="RaisedVia"/>, and hence <see cref="EventGuestId"/> being optional,
/// since an incident may concern the delegation rather than one person.
/// </summary>
public class Incident : Entity
{
    public int EventId { get; set; }

    /// <summary>The delegate it concerns. Null when it is about the delegation as
    /// a whole.</summary>
    public int? EventGuestId { get; set; }

    /// <summary>Core.Constants.IncidentCategories — late / lost / medical / other.</summary>
    public string Category { get; set; }

    /// <summary>Core.Constants.IncidentSeverities — low / medium / high.</summary>
    public string Severity { get; set; } //Enums

    /// <summary>Core.Constants.IncidentStatuses — open / in_progress / resolved /
    /// escalated.</summary>
    public string Status { get; set; }

    public string Description { get; set; }

    public int? RaisedBy { get; set; }

    /// <summary>Core.Constants.IncidentChannels — app / portal. Kept because "the
    /// delegate reported this themselves" and "an officer logged it for them" are
    /// materially different when reviewing a mission afterwards.</summary>
    public string RaisedVia { get; set; }

    public int? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public virtual Event Event { get; set; }
    public virtual EventGuest EventGuest { get; set; }
    public virtual User RaisedByUser { get; set; }
    public virtual User ResolvedByUser { get; set; }
}
