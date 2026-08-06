using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// A globally-defined offerable service — "Flight", "Transport", "Lounge Access".
    /// Created once and usable by every event, Fixed or Flexible.
    /// See docs/service-levels-v2.md.
    /// </summary>
    public class Service : Entity
    {
        /// <summary>Stable slug, unique across the catalogue.</summary>
        public string Code { get; set; }

        public string Name { get; set; }
        public string NameAr { get; set; }
        public string Description { get; set; }

        /// <summary>Icon key the UI renders on the service chip.</summary>
        public string Icon { get; set; }

        public int SortOrder { get; set; }

        /// <summary>
        /// Retired services stop being assignable but stay readable, so guest
        /// entries already completed against them keep rendering.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// The form a user completes for a guest, as sections of fields.
        /// Read and written through <c>Core.Constants.ServiceFormSchema</c>.
        /// </summary>
        public string FormSchemaJson { get; set; }

        public virtual ICollection<ServiceLevelService> ServiceLevels { get; set; } = new List<ServiceLevelService>();
        public virtual ICollection<GuestServiceEntry> GuestEntries { get; set; } = new List<GuestServiceEntry>();
    }
}
