using System.Collections.Generic;
using System;

namespace DomainPersistence.Entities
{
    public class Guest : Entity
    {
        public string FirstName { get; set;}
        public string LastName { get; set;}
        public string Email { get; set;}
        public int? NationalityId { get; set;}
        public string PhotoUrl { get; set; }

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
