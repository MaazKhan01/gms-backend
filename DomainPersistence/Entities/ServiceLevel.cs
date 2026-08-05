using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// A per-event guest grade — "Gold", "VVIP", "Delegate" — replacing the old
    /// hardcoded <c>Guest.Tier</c> string. A level is a bundle: it links to one or
    /// more <see cref="Service"/> rows, each carrying the field VALUES for that
    /// level (see <see cref="ServiceLevelService.FieldValuesJson"/>), so every
    /// guest on the level inherits the same entitlements.
    /// </summary>
    public class ServiceLevel : Entity
    {
        public int EventId { get; set; }
        public string Name { get; set; }
        public string NameAr { get; set; }

        // Slug mirrored onto Guest.Tier so every existing string-based consumer
        // (chips, CSV export, invitation-template targeting, VIP-app seating
        // category) keeps working untouched. See GuestService for the sync.
        public string Code { get; set; }

        public string Description { get; set; }

        // Chip/badge colour, e.g. "#e0b864". Replaces the hardcoded TIER_COLOR
        // maps the frontend used to carry in two separate files.
        public string Color { get; set; }

        // 1 = highest grade. Drives display order and "is this an upgrade?" reads.
        public int SortOrder { get; set; }

        // ── Rules (validated on guest assign, overridable — see
        // PermissionCodes.ServiceLevelsOverrideRules) ────────────────────────
        /// <summary>Max guests on this level for the event. Null = unlimited.</summary>
        public int? Capacity { get; set; }

        /// <summary>JSON array of guest field keys that must be filled before a
        /// guest can be placed on this level, e.g. ["photoUrl","nationalityId"].
        /// Read/written via Core.Constants.ServiceLevelRules.</summary>
        public string RequiredGuestFieldsJson { get; set; }

        public virtual Event Event { get; set; }
        public virtual ICollection<ServiceLevelService> Services { get; set; } = new List<ServiceLevelService>();
        public virtual ICollection<Guest> Guests { get; set; } = new List<Guest>();
    }
}
