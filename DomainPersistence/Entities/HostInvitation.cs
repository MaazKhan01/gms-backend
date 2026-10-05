using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Deliberately not called "Invitation": <see cref="Entities.Invitation"/> is the
/// outbound guest RSVP and is an unrelated concept. Both exist side by side.
/// </summary>
public class HostInvitation : Entity
{
    public string HostEmail { get; set; }

    /// <summary>The organisation that invited us. An Organizations row — the
    /// same directory guests are affiliated with.</summary>
    public int? HostOrganizationId { get; set; }

    /// <summary>
    /// The organisation's name, copied from <see cref="HostOrganizationId"/> on
    /// every save. Denormalised on purpose: the nomination letter and the
    /// converted mission both need the name without a join, and an invitation
    /// logged before the directory existed still has one.
    /// </summary>
    public string HostOrganization { get; set; }

    public string MissionTitle { get; set; }

    /// <summary>Where the host's event takes place. Always a Locations row — the
    /// place is created there at log time if it does not exist yet. Copied onto
    /// the mission at conversion.</summary>
    public int? DestinationId { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    // Both nullable: a host letter often states neither. A non-nullable DateOnly
    // would default to 0001-01-01, which reads as a real deadline already past.
    public int? HeadcountCap { get; set; }

    public DateOnly? ResponseDeadline { get; set; }
    public string AttachmentUrl { get; set; }
    public string Status { get; set; }

    public string Notes { get; set; }

    public int? ConvertedEventId { get; set; }

    public virtual Location Destination { get; set; }
    public virtual Organization HostOrganizationRef { get; set; }
    public virtual Event ConvertedEvent { get; set; }
}
