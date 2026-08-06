using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// A globally-defined guest grade — "VVIP", "Gold", "Delegate". Bundles the
    /// services a guest on that grade receives, in the order they must be
    /// completed for a Fixed event. See docs/service-levels-v2.md.
    /// </summary>
    public class ServiceLevel : Entity
    {
        /// <summary>
        /// Stable slug, unique across the catalogue. Mirrored onto Guest.Tier so
        /// the legacy string consumers (chips, CSV export, invitation targeting)
        /// keep working.
        /// </summary>
        public string Code { get; set; }

        public string Name { get; set; }
        public string NameAr { get; set; }
        public string Description { get; set; }

        /// <summary>Chip/badge colour, e.g. "#e0b864".</summary>
        public string Color { get; set; }

        /// <summary>1 = highest grade. Drives display order.</summary>
        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// JSON array of guest field keys that must be filled before a guest can
        /// be placed on this level, e.g. ["photoUrl","nationalityId"]. Validates
        /// the guest record itself, so it stays meaningful now that levels are
        /// global. Read/written via Core.Constants.ServiceLevelRules.
        /// </summary>
        public string RequiredGuestFieldsJson { get; set; }

        public virtual ICollection<ServiceLevelService> Services { get; set; } = new List<ServiceLevelService>();
        public virtual ICollection<Guest> Guests { get; set; } = new List<Guest>();
    }
}
