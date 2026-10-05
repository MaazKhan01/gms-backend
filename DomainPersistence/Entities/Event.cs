using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

public partial class Event : Entity
{
    public string Title { get; set; }
    public string Type { get; set; }            // Conference | Forum | Summit | Gala | ...
    public string VenueName { get; set; }
    public int? VenueId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; }          // planning | active | completed | cancelled
    public string AppKey { get; set; }          // url-safe slug derived from the title

    /// <summary>
    /// fixed | flexible — see <c>Core.Constants.EventGuestModels</c>.
    /// Distinct from <see cref="Type"/>, which is the kind of occasion
    /// (Conference/Forum/…): this decides whether the event runs the Service
    /// Level flow with its rules, or the older unrestricted one.
    /// </summary>
    public string GuestModel { get; set; }
    public string ImageUrl { get; set; }
    public string AttachmentUrl { get; set; }

    // ── Mission fields
    /// <summary>Where the delegation travels to. Always a Locations row — never a
    /// free-text place. Null for a locally hosted event with no destination.</summary>
    public int? DestinationId { get; set; }

    public int? DelegationCap { get; set; }

    /// <summary>Core.Constants.DestinationTiers — regional / international_a|b|c.
    /// Drives per-diem banding and approval routing downstream.</summary>
    public string DestinationTier { get; set; }

    /// <summary>The budget line the mission is charged to. A free-text reference
    /// into the finance system, which is outside DMS — so it is recorded, not
    /// validated.</summary>
    public string CostCenter { get; set; }

    /// <summary>Core.Constants.FundingModels — org_paid / hosted / mixed. Who
    /// pays decides which services are booked at all.</summary>
    public string FundingModel { get; set; }

    /// <summary>The organisation hosting the mission — an Organizations row.</summary>
    public int? HostOrganizationId { get; set; }

    /// <summary>The organisation's name, copied from
    /// <see cref="HostOrganizationId"/> on save. Denormalised for the same
    /// reason as HostInvitation.HostOrganization: the letter needs the name
    /// without a join, and older missions only ever had one.</summary>
    public string HostName { get; set; }

    public string HostEmail { get; set; }

    public int? HostInvitationId { get; set; }
    
    public virtual Venue? Venue { get; set;}
    public virtual Location Destination { get; set; }
    public virtual Organization HostOrganization { get; set; }
    public virtual HostInvitation HostInvitation { get; set; }
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
