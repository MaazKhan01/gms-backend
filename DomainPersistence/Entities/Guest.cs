using System.Collections.Generic;
using System;

namespace DomainPersistence.Entities
{
    /// <summary>
    /// A person, not a booking. Identified by <see cref="Email"/>, which is required
    /// and unique — the same human invited to five events is ONE row here with five
    /// <see cref="EventGuest"/> rows hanging off it.
    /// </summary>
    /// <remarks>
    /// Only person-level facts belong here. Anything that could differ per event
    /// (service level, guest type, organization, accreditation requirement,
    /// self-service permissions) lives on <see cref="EventGuest"/>, and every
    /// per-event child record (sessions, invitation, flights, hotel, transport,
    /// seating, service entries) keys off that row rather than this one.
    /// <para>
    /// Tier and ArrivalDate/DepartureDate used to live here and are gone: grades are
    /// Service Levels now, and arrival/departure are properties of the Flight
    /// booking, which already carries them per leg.
    /// </para>
    /// </remarks>
    public class Guest : Entity
    {
        public string FirstName { get; set;}
        public string LastName { get; set;}

        /// <summary>The person's identity key — required, unique, and the value the
        /// VIP app's OTP login resolves against. Changing it re-identifies the
        /// person, so the API treats it as immutable after creation.</summary>
        public string Email { get; set;}

        public int? NationalityId { get; set;}
        public string PhotoUrl { get; set; }

        // ── VIP-app account settings (person-level, not per event) ──────────────
        public string PreferencesJson { get; set; }
        public bool NotificationsEnabled { get; set; } = true;
        public string Language { get; set; }

        public int UserId { get; set; }

        public virtual Nationality Nationality { get; set; }
        public virtual User User { get; set; }

        /// <summary>Every event this person participates in.</summary>
        public virtual ICollection<EventGuest> EventGuests { get; set; } = new List<EventGuest>();
    }
}
