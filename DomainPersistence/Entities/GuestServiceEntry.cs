using System;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// One completed instance of a <see cref="Service"/> for a guest. A guest may
    /// have several entries for the same service (two flights, two transfers).
    /// </summary>
    /// <remarks>
    /// Absence means pending: a service with no entry has not been started, so no
    /// row is written for work that has not happened.
    /// </remarks>
    public class GuestServiceEntry : Entity
    {
        public int EventGuestId { get; set; }
        public int ServiceId { get; set; }

        /// <summary>pending | completed — see Core.Constants.GuestServiceStatus.</summary>
        public string Status { get; set; }

        /// <summary>
        /// Flat { fieldKey: value } map against the service's form schema. Values
        /// are stored as strings; the schema's field type tells the UI how to
        /// render and parse them.
        /// </summary>
        public string ValuesJson { get; set; }

        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }

        public virtual EventGuest EventGuest { get; set; }
        public virtual Service Service { get; set; }
    }
}
