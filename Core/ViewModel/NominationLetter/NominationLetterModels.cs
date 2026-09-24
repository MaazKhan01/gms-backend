using System;
using System.Collections.Generic;

namespace Core.ViewModel.NominationLetter;

/// <summary>The letter's current state for one mission, with its versions and log.</summary>
public class NominationLetterResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string MissionTitle { get; set; }

    /// <summary>Core.Constants.NominationLetterStatuses.</summary>
    public string Status { get; set; }
    public int CurrentVersion { get; set; }
    public string Language { get; set; }

    public DateTime? GeneratedOn { get; set; }
    public DateTime? SentOn { get; set; }
    public DateTime? RespondedOn { get; set; }
    public string MessageId { get; set; }
    public string HostNotes { get; set; }

    public string HostName { get; set; }
    public string HostEmail { get; set; }

    /// <summary>Roster size at the latest generation — not the live roster.
    /// A difference between the two is what tells you to re-issue.</summary>
    public int VersionRosterCount { get; set; }
    public int LiveRosterCount { get; set; }
    /// <summary>True when the roster has changed since the current version was
    /// generated, so what the host holds is out of date.</summary>
    public bool RosterChangedSinceGenerated { get; set; }

    public List<NominationLetterVersionResponse> Versions { get; set; } = new();
    public List<NominationLetterHistoryResponse> History { get; set; } = new();
}

public class NominationLetterVersionResponse
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public DateTime GeneratedOn { get; set; }
    public string Language { get; set; }
    public string DocumentUrl { get; set; }
    public int RosterCount { get; set; }
}

/// <summary>One version's pinned roster — who the host was actually sent.</summary>
public class NominationLetterVersionDetailResponse : NominationLetterVersionResponse
{
    public List<NominationLetterRosterEntry> Roster { get; set; } = new();
}

/// <summary>A roster line as it stood when the version was generated. Snapshotted,
/// not joined: the live roster moves on and v1 must keep showing what was sent.</summary>
public class NominationLetterRosterEntry
{
    public Guid ParticipationId { get; set; }
    public string FullName { get; set; }
    public string JobTitle { get; set; }
    public string MissionRole { get; set; }
    public string Subgroup { get; set; }
    public string Department { get; set; }
    public string Nationality { get; set; }
    public string PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
}

public class NominationLetterHistoryResponse
{
    public DateTime OccurredOn { get; set; }
    public string Action { get; set; }
    public string Note { get; set; }
    public string ActorName { get; set; }
}

public class GenerateLetterRequest
{
    public Guid EventId { get; set; }
    /// <summary>en | ar. Defaults to en.</summary>
    public string Language { get; set; }
    /// <summary>Optional URL of the rendered document, when one is produced
    /// outside this API.</summary>
    public string DocumentUrl { get; set; }
}

public class SendLetterRequest
{
    public Guid EventId { get; set; }
    /// <summary>Overrides the mission's host email for this send only.</summary>
    public string HostEmail { get; set; }
    /// <summary>Message id from the mail system, so a reply can be traced back.</summary>
    public string MessageId { get; set; }
    public string Note { get; set; }
}

public class LetterResponseRequest
{
    public Guid EventId { get; set; }
    /// <summary>What the host said. Required when requesting changes.</summary>
    public string Note { get; set; }
}
