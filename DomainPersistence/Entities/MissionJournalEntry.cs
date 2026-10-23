using System;

namespace DomainPersistence.Entities;

/// <summary>
/// One item a delegate captured during a mission — a note, photo, video or
/// document — filed under a day of the mission. Ticked items
/// (<see cref="IncludedInReport"/>) are the evidence attached to that delegate's
/// post-mission report.
///
/// Read-only once the mission is closed; that is enforced in the service, not
/// here, so a coordinator can still correct a row from the database.
/// </summary>
public class MissionJournalEntry : Entity
{
    public int EventGuestId { get; set; }

    /// <summary>The mission day this belongs to. Must fall inside the mission's
    /// date range.</summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>The programme item it is about, when the delegate added it from
    /// one. Null = about the day in general.</summary>
    public int? SessionId { get; set; }

    /// <summary>Core.Constants.JournalEntryTypes — note / photo / video / document.</summary>
    public string Type { get; set; }

    /// <summary>Note title, or the uploaded file's original name.</summary>
    public string Title { get; set; }

    public string Body { get; set; }

    /// <summary>Blob URL, stored bare — BlobSasMiddleware signs it on the way out.
    /// Null for a note.</summary>
    public string FileUrl { get; set; }
    public long? FileSizeBytes { get; set; }

    public bool IncludedInReport { get; set; }

    /// <summary>Core.Constants.JournalVisibilities — private (only the delegate)
    /// or shared (coordinator and head of delegation can see it).</summary>
    public string Visibility { get; set; } = "shared";

    public virtual EventGuest EventGuest { get; set; }
    public virtual Session Session { get; set; }
}
