using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>
/// The official letter naming the delegation, sent to the host. One per mission:
/// the row carries the CURRENT state, while every issued revision is kept in
/// <see cref="Versions"/> and every state change in <see cref="History"/>.
///
/// Re-issuing after the host asks for changes produces a new version rather than
/// overwriting this row — the host may still be holding v1.
/// </summary>
public class NominationLetter : Entity
{
    public int EventId { get; set; }

    /// <summary>Core.Constants.NominationLetterStatuses — not_generated / draft /
    /// sent / changes_requested / acknowledged.</summary>
    public string Status { get; set; }

    /// <summary>Highest version issued so far; 0 until first generated.</summary>
    public int CurrentVersion { get; set; }

    /// <summary>en / ar — the language the current version was rendered in.</summary>
    public string Language { get; set; }

    public DateTime? GeneratedOn { get; set; }
    public DateTime? SentOn { get; set; }

    /// <summary>When the host last replied, whether acknowledging or asking for
    /// changes.</summary>
    public DateTime? RespondedOn { get; set; }

    /// <summary>Message id of the outbound email, so a reply can be traced back.</summary>
    public string MessageId { get; set; }

    /// <summary>Free text from the host's reply — what they want changed.</summary>
    public string HostNotes { get; set; }

    public virtual Event Event { get; set; }
    public virtual ICollection<NominationLetterVersion> Versions { get; set; } = new List<NominationLetterVersion>();
    public virtual ICollection<NominationLetterHistory> History { get; set; } = new List<NominationLetterHistory>();
}

/// <summary>One issued revision of a nomination letter, pinned to the roster it
/// was generated from.</summary>
public class NominationLetterVersion : Entity
{
    public int NominationLetterId { get; set; }

    public int Version { get; set; }

    public DateTime GeneratedOn { get; set; }

    public string Language { get; set; }

    /// <summary>Rendered PDF, as a blob URL. Null if generation is deferred.</summary>
    public string DocumentUrl { get; set; }

    /// <summary>The roster AS IT WAS when this version was produced. Snapshotted
    /// rather than joined: the live roster moves on, and v1 has to keep showing
    /// the names the host was actually sent.</summary>
    public string RosterSnapshotJson { get; set; }

    public virtual NominationLetter NominationLetter { get; set; }
}

/// <summary>Append-only log of everything that happened to a nomination letter —
/// generated, sent, host replied, re-issued. Never updated or deleted.</summary>
public class NominationLetterHistory : Entity
{
    public int NominationLetterId { get; set; }

    public DateTime OccurredOn { get; set; }

    /// <summary>Short description of what happened, e.g. "Letter generated (v2)".</summary>
    public string Action { get; set; }

    public string Note { get; set; }

    /// <summary>Who did it. Null for anything the system did on its own.</summary>
    public int? ActorId { get; set; }

    public virtual NominationLetter NominationLetter { get; set; }
    public virtual User Actor { get; set; }
}
