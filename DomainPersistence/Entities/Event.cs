using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

public partial class Event : Entity
{
    public string Title { get; set; }
    public string Type { get; set; }            // Conference | Forum | Summit | Gala | ...
    public string Theme { get; set; }
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

    // Branding (replaces the localStorage "gms-event-themes" on the frontend)
    public string ImageUrl { get; set; }
    public string ThemeAccent { get; set; }
    public string ThemeSecondary { get; set; }
    public string LogoDarkUrl { get; set; }
    public string LogoLightUrl { get; set; }

    public virtual Venue? Venue { get; set;}
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
