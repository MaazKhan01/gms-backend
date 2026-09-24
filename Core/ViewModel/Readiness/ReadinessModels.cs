using System;
using System.Collections.Generic;

namespace Core.ViewModel.Readiness;

/// <summary>One delegate's readiness to travel.</summary>
public class ReadinessResponse
{
    /// <summary>The participation id — what the waive endpoints take.</summary>
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string FullName { get; set; }
    public string MissionRoleName { get; set; }
    public string Subgroup { get; set; }

    /// <summary>True when every item is met or waived.</summary>
    public bool TravelReady { get; set; }
    /// <summary>True when readiness was only reached because something was waived.
    /// Ready is ready, but the distinction matters when reviewing a mission.</summary>
    public bool ReadyWithWaivers { get; set; }

    public List<ReadinessItemState> Items { get; set; } = new();
}

public class ReadinessItemState
{
    /// <summary>Core.Constants.ReadinessItems — passport / visa / flight /
    /// accommodation / transport.</summary>
    public string Key { get; set; }

    /// <summary>met | waived | missing.</summary>
    public string Status { get; set; }

    /// <summary>Why it is not met, or what is on file. Display text only.</summary>
    public string Detail { get; set; }

    // ── Present only when waived ─────────────────────────────────────────
    public Guid? WaiverId { get; set; }
    public string WaiverReason { get; set; }
    public string WaivedByName { get; set; }
    public DateTime? WaivedAt { get; set; }
}

/// <summary>Mission-level counts for the screen header.</summary>
public class ReadinessSummaryResponse
{
    public int Total { get; set; }
    public int TravelReady { get; set; }
    public int NotReady { get; set; }
    public int ReadyWithWaivers { get; set; }
    /// <summary>How many delegates are missing each item, keyed by item.</summary>
    public Dictionary<string, int> MissingByItem { get; set; } = new();
}

public class WaiveReadinessRequest
{
    /// <summary>The participation id.</summary>
    public Guid Id { get; set; }
    public string ItemKey { get; set; }
    /// <summary>Required — this log is the only record of why a gate was skipped.</summary>
    public string Reason { get; set; }
}
