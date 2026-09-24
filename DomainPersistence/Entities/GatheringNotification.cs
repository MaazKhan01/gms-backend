using System;

namespace DomainPersistence.Entities;

/// <summary>
/// A coordinator's broadcast to the delegation on the ground ("Bus departs the
/// lobby in 10 minutes").
///
/// This is the coordinator's OUTBOX, not a delivery mechanism: the message still
/// fans out through the existing Notification / Device / push machinery, one row
/// per recipient. Kept separate because the existing table answers "what did this
/// person receive", and the ops screen needs "what did I send to the group".
/// </summary>
public class GatheringNotification : Entity
{
    public int EventId { get; set; }

    public string Message { get; set; }

    /// <summary>Null targets the whole delegation; otherwise the subgroup name as
    /// stored on EventGuest.Subgroup.</summary>
    public string Subgroup { get; set; }

    /// <summary>How many delegates it went out to, counted at send time — the
    /// roster may change afterwards.</summary>
    public int RecipientCount { get; set; }

    public int? SentBy { get; set; }
    public DateTime SentAt { get; set; }

    public virtual Event Event { get; set; }
    public virtual User SentByUser { get; set; }
}
