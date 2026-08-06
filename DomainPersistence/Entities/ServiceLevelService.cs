using System;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// Assignment: which <see cref="Service"/> a <see cref="ServiceLevel"/> includes.
    /// Configuration only — the completed values live on
    /// <see cref="GuestServiceEntry"/>, against the guest the form was filled in for.
    /// </summary>
    public class ServiceLevelService : Entity
    {
        public int ServiceLevelId { get; set; }
        public int ServiceId { get; set; }

        /// <summary>
        /// Position in the level's sequence. For a Fixed event this is the order
        /// services must be completed in; for Flexible it is display order only.
        /// </summary>
        public int SortOrder { get; set; }

        public virtual ServiceLevel ServiceLevel { get; set; }
        public virtual Service Service { get; set; }
    }
}
