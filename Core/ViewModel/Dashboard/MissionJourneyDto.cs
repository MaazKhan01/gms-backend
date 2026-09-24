using System;
using System.Collections.Generic;

namespace Core.ViewModel.Dashboard;

/// <summary>
/// The mission read as one sequence: where it has got to in each phase, and what
/// is holding it up. Everything here is derived from rows the dashboard already
/// loads or one extra count, so the strip costs no round trips of its own.
///
/// Only phases the backend actually models appear. A phase with no endpoints yet
/// carries <c>Modelled = false</c> rather than a fabricated zero — "not built"
/// and "nothing done" look identical at 0% and mean very different things.
/// </summary>
public class MissionJourneyDto
{
    public List<MissionJourneyStepDto> Steps { get; set; } = new();

    /// <summary>The delegation cap from the host, when one was given.</summary>
    public int? DelegationCap { get; set; }
    public int RosterCount { get; set; }
    public string HostName { get; set; }
}

public class MissionJourneyStepDto
{
    /// <summary>The BRD phase number, not the step's position — phases the system
    /// does not model are absent, so these are not contiguous.</summary>
    public int Phase { get; set; }

    /// <summary>The menu code this step links to, so the client navigates by the
    /// same code everything else is keyed on.</summary>
    public string Code { get; set; }

    public string Title { get; set; }
    public string TitleAr { get; set; }

    /// <summary>0–100. Meaningless when <see cref="Modelled"/> is false.</summary>
    public int Percent { get; set; }

    /// <summary>The one number worth reading at a glance — "12/15 Roster".</summary>
    public string Stat { get; set; }

    /// <summary>Set only when something needs attention. Null is the good case.</summary>
    public string Flag { get; set; }

    public bool Modelled { get; set; } = true;
}
