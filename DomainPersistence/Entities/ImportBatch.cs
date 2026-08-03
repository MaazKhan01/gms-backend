using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>
/// Tracks one bulk-import run (Events Excel, Guests CSV) processed by a
/// Hangfire background job — the HTTP request that uploads the file only
/// creates this row and enqueues the job, so the user never waits on it.
/// </summary>
public class ImportBatch : Entity
{
    public string Kind { get; set; }          // "events" | "guests"
    public int? EventId { get; set; }         // scope for a guests import; null for events
    public string Status { get; set; }        // "queued" | "processing" | "completed" | "failed"
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public string FileUrl { get; set; }
    public string ErrorMessage { get; set; }  // set only on a hard failure (e.g. unreadable file)
    public int CreatedByUserId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual Event? Event { get; set; }
    public virtual ICollection<ImportBatchRow> Rows { get; set; } = new List<ImportBatchRow>();
}
