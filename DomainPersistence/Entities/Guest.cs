using System.Collections.Generic;
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
        public string Organization { get; set; }
        public int? OrganizationId { get; set; }
        public int? NationalityId { get; set;}

         public string Tier { get; set;}

        // The guest's grade for this event. Nullable so pre-existing rows and CSV
        public int? ServiceLevelId { get; set; }

        // Set when an authorised user pushed this assignment through despite a
        // failing Service Level rule (capacity full / missing required fields).
        // Kept for audit — diplomatic events need to show who waived what.
        public bool ServiceLevelRulesOverridden { get; set; }
        public string ServiceLevelOverrideReason { get; set; }
        public string PreferencesJson { get; set; }
        public bool NotificationsEnabled { get; set; } = true;
        public string Language { get; set; }
        public DateOnly? ArrivalDate { get; set; }
        public DateOnly? DepartureDate { get; set; }
        public string PhotoUrl { get; set; }
        // Set at creation — does this guest need an accreditation badge at all?
        // The actual issue/revoke lifecycle lives on the Invitation row.
        public bool AccreditationRequired { get; set; }

        public string AllowedServicesJson { get; set; }

         public int UserId { get; set; }

        public virtual ICollection<GuestSession> GuestSessions { get; set; } = new List<GuestSession>();
        public virtual Nationality Nationality { get; set; }
        public virtual Organization OrganizationRef { get; set; }
        public virtual ServiceLevel ServiceLevel { get; set; }
        public virtual ICollection<GuestServiceEntry> ServiceEntries { get; set; } = new List<GuestServiceEntry>();
        public virtual Event Event { get; set; }
        public virtual User User { get; set; }
    }
}
