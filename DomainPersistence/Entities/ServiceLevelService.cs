using System;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// Join row: which <see cref="Service"/> a <see cref="ServiceLevel"/> includes,
    /// plus the VALUES for that service's dynamic fields at this level.
    /// </summary>
    /// <remarks>
    /// The values live here (per level), not per guest — "Gold includes Lounge
    /// Access with Lounge Name = Al Mourjan" is configured once and inherited by
    /// every Gold guest.
    /// </remarks>
    public class ServiceLevelService : Entity
    {
        public int ServiceLevelId { get; set; }
        public int ServiceId { get; set; }

        // JSON object keyed by the parent Service's FieldsSchema field keys —
        // e.g. {"loungeName":"Al Mourjan","guestCount":"2"}. Values are stored as
        // strings; the schema's `type` tells the UI how to render/parse them.
        public string FieldValuesJson { get; set; }

        public virtual ServiceLevel ServiceLevel { get; set; }
        public virtual Service Service { get; set; }
    }
}
