using System;

namespace DomainPersistence.Entities
{
    public class Guest : Entity
    {
        public string FirstName { get; set;}
        public string LastName { get; set;}
        public string Email { get; set;}
        public int EventId { get; set;}
        public string GuestType { get; set;}
        // Free-text fallback — kept in sync with OrganizationRef.Name whenever
        // OrganizationId is set (see GuestService), so every existing consumer
        // that just reads this string (search, CSV export, travel rows,
        // dashboard) keeps working unchanged. CSV import still writes here
        // directly with no OrganizationId, since it has no id to resolve.
        public string Organization { get; set; }
        public int? OrganizationId { get; set; }
        public int? NationalityId { get; set;}
        public string Tier { get; set;}
        public string PreferencesJson { get; set; }
        public bool NotificationsEnabled { get; set; } = true;
        public string Language { get; set; }
        public DateOnly? ArrivalDate { get; set; }
        public DateOnly? DepartureDate { get; set; }
        public string PhotoUrl { get; set; }
        // Set at creation — does this guest need an accreditation badge at all?
        // The actual issue/revoke lifecycle lives on the Invitation row.
        public bool AccreditationRequired { get; set; }

        public virtual ICollection<GuestSession> GuestSessions { get; set; } = new List<GuestSession>();
        public virtual Nationality Nationality { get; set; }
        public virtual Organization OrganizationRef { get; set; }
        public virtual Event Event { get; set; }
    }
}
