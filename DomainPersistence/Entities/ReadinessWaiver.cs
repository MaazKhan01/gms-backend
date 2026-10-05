using System;

namespace DomainPersistence.Entities;

/// <summary>
/// A coordinator's override of one red readiness item for one delegate, with a
/// reason that is not optional — this log is the only record of why a delegate
/// was allowed to travel without something the checklist demanded.
///
/// There is no MissionId: the mission is implied by <see cref="EventGuestId"/>,
/// which makes "one waiver per delegate per mission per item" a uniqueness
/// constraint rather than a convention.
/// </summary>
public class ReadinessWaiver : Entity
{
    public int EventGuestId { get; set; }

    /// <summary>Core.Constants.ReadinessItems — passport / visa / flight /
    /// accommodation / transport.</summary>
    public string ItemKey { get; set; }

    /// <summary>Required. "Visa not needed for this nationality", "travelling on a
    /// second passport", and so on.</summary>
    public string Reason { get; set; }

    public int? WaivedBy { get; set; }
    public DateTime WaivedAt { get; set; }

    public virtual EventGuest EventGuest { get; set; }
    public virtual User WaivedByUser { get; set; }
}
