using System;
using System.Collections.Generic;

namespace Core.ViewModel.Incident;

public class IncidentResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    /// <summary>The participation it concerns. Null when it is about the
    /// delegation as a whole.</summary>
    public Guid? DelegateId { get; set; }
    public string DelegateName { get; set; }
    public string Subgroup { get; set; }

    public string Category { get; set; }
    public string Severity { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }

    /// <summary>app = the delegate reported it themselves; portal = an officer
    /// logged it for them.</summary>
    public string RaisedVia { get; set; }
    public string RaisedByName { get; set; }
    public DateTime RaisedAt { get; set; }

    public string ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>True while the incident still needs someone's attention —
    /// escalated counts as open.</summary>
    public bool IsOpen { get; set; }
}

public class CreateIncidentRequest
{
    public Guid EventId { get; set; }
    /// <summary>Optional — omit for an incident about the delegation itself.</summary>
    public Guid? DelegateId { get; set; }
    public string Category { get; set; }
    public string Severity { get; set; }
    public string Description { get; set; }
}

public class UpdateIncidentRequest
{
    public Guid Id { get; set; }
    public Guid? DelegateId { get; set; }
    public string Category { get; set; }
    public string Severity { get; set; }
    public string Description { get; set; }
}

public class ChangeIncidentStatusRequest
{
    public Guid Id { get; set; }
    /// <summary>open / in_progress / resolved / escalated.</summary>
    public string Status { get; set; }
    /// <summary>Appended to the description as a dated line. Required when
    /// resolving — the log has to say how it ended.</summary>
    public string Note { get; set; }
}

/// <summary>Counts for the ops screen header.</summary>
public class IncidentSummaryResponse
{
    public int Total { get; set; }
    public int Open { get; set; }
    public int Resolved { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> BySeverity { get; set; } = new();
    public Dictionary<string, int> ByCategory { get; set; } = new();
}
