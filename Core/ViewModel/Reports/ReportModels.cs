using System;
using System.Collections.Generic;

namespace Core.ViewModel.Reports;

// ─────────────────────────────────────────────────────────────────────────────
// Phase 9 — Reports & Close.
//
// Two documents, deliberately separate. Each delegate writes their own
// post-mission report; the Head of Delegation assembles those into one Combined
// Mission Report, and Protocol approves and publishes it. Reviewing and
// approving are different people answering different questions, so the combined
// report records both sign-offs rather than one "done" flag.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One delegate's report, with the trip facts DMS already knows. The facts are
/// read from the booking records on every request rather than copied into the
/// report, so a late correction to a flight shows up without anyone re-saving.
/// </summary>
public class PostMissionReportResponse
{
    /// <summary>The participation id — a report belongs to a person ON a mission.</summary>
    public Guid ParticipationId { get; set; }
    public Guid PersonId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string JobTitle { get; set; }
    public string DepartmentName { get; set; }
    public string MissionRoleName { get; set; }
    public string Subgroup { get; set; }

    /// <summary>Core.Constants.PostMissionReportStatuses.</summary>
    public string Status { get; set; }
    public string Narrative { get; set; }
    public DateTime? SubmittedOn { get; set; }
    public int NudgeCount { get; set; }
    public DateTime? LastNudgeOn { get; set; }

    /// <summary>What DMS fills in for them, so the delegate only writes the part
    /// only they can write.</summary>
    public TripFacts Facts { get; set; } = new();
}

/// <summary>The mission as this delegate actually travelled it.</summary>
public class TripFacts
{
    public string MissionTitle { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Destination { get; set; }
    public string HostName { get; set; }

    public string Hotel { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }

    /// <summary>Flight legs, most recent first, as "QR123 DOH → CDG, 10 Feb".</summary>
    public List<string> Flights { get; set; } = new();
    public List<string> Transport { get; set; } = new();
    /// <summary>Sessions and meetings on this mission the delegate was down for.</summary>
    public List<string> Attended { get; set; } = new();
}

public class PostMissionReportSummary
{
    public int Total { get; set; }
    public int Submitted { get; set; }
    public int Approved { get; set; }
    public int Outstanding { get; set; }
    /// <summary>Outstanding reports that have already been chased at least once —
    /// the ones a further nudge is unlikely to shift on its own.</summary>
    public int NudgedAndStillOutstanding { get; set; }
}

public class SaveReportRequest
{
    /// <summary>The participation id.</summary>
    public Guid Id { get; set; }
    public string Narrative { get; set; }
    /// <summary>False saves a draft; true also submits it.</summary>
    public bool Submit { get; set; }
}

public class NudgeReportsRequest
{
    /// <summary>Participation ids. Empty means every outstanding report on the
    /// mission — chasing the stragglers is the normal case.</summary>
    public List<Guid> Ids { get; set; } = new();
    public Guid EventId { get; set; }
    public string Note { get; set; }
}

public class NudgeResult
{
    public int Sent { get; set; }
    public int Skipped { get; set; }
    /// <summary>Why each was skipped — already in, or no email on file.</summary>
    public List<string> Notes { get; set; } = new();
}

// ── Combined report ──────────────────────────────────────────────────────────

public class CombinedReportResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string MissionTitle { get; set; }

    /// <summary>Core.Constants.CombinedReportStatuses.</summary>
    public string Status { get; set; }
    public string ContentHtml { get; set; }

    public DateTime? AssembledOn { get; set; }
    public string ReviewedByName { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public string PublishedUrl { get; set; }

    /// <summary>True once the mission itself has been closed.</summary>
    public bool MissionClosed { get; set; }
    public string MissionStatus { get; set; }

    /// <summary>How the delegates' reports stand, so the screen can say what
    /// assembling now would leave out.</summary>
    public PostMissionReportSummary Reports { get; set; } = new();

    /// <summary>True when the roster has changed or a report has been submitted
    /// since the body was assembled — what is on screen is then out of date.</summary>
    public bool StaleSinceAssembled { get; set; }
}

public class SaveCombinedReportRequest
{
    public Guid EventId { get; set; }
    public string ContentHtml { get; set; }
}

public class CombinedReportActionRequest
{
    public Guid EventId { get; set; }
    /// <summary>Recorded on the history of whichever step this is.</summary>
    public string Note { get; set; }
    /// <summary>Publish only: the archived PDF's URL, when one is produced
    /// outside this API.</summary>
    public string PublishedUrl { get; set; }
}
