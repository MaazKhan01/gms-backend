
namespace DomainPersistence.Entities
{
    public class Guest : Entity
    {
        public string FirstName { get; set;}
        public string LastName { get; set;}
        public string Email { get; set;}
        public int EventId { get; set;}
        public string GuestType { get; set;}
        public string Organization { get; set; }
        public int? NationalityId { get; set;}
        public string Tier { get; set;}
        public string PreferencesJson { get; set; }
        public bool NotificationsEnabled { get; set; } = true;
        public string Language { get; set; }

        public virtual ICollection<GuestSession> GuestSessions { get; set; } = new List<GuestSession>();
        public virtual Nationality Nationality { get; set; }
        public virtual Event Event { get; set; }
    }
}
