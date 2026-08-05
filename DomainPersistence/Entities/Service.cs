using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// An offerable service in one event's catalog — "Lounge Access", "Airport
    /// Transfer", "Prayer Room Access". Per-event (not a global lookup): event A
    /// defines its own catalog, independent of event B.
    /// </summary>
    /// <remarks>
    /// NOT the same concept as <c>Core.Constants.GuestServiceType</c>
    /// (Flight/Accommodation/Transport), which is the fixed list of things a
    /// guest may self-REQUEST from the VIP app and is stored on
    /// <c>Guest.AllowedServicesJson</c>. That one is a permission list; this is
    /// an admin-authored entitlement catalog. They are deliberately unrelated.
    /// </remarks>
    public class Service : Entity
    {
        public int EventId { get; set; }
        public string Name { get; set; }
        public string NameAr { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; }

        // JSON array of field definitions this service asks for when attached to
        // a Service Level — e.g. [{"key":"loungeName","label":"Lounge Name",
        // "type":"text","required":true}]. Dynamic by design: each service
        // defines its own attributes, so there is no fixed column set.
        // Read/written via Core.Constants.ServiceFieldSchema, never raw.
        public string FieldsSchema { get; set; }

        public virtual Event Event { get; set; }
        public virtual ICollection<ServiceLevelService> ServiceLevels { get; set; } = new List<ServiceLevelService>();
    }
}
