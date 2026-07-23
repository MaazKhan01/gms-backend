
namespace DomainPersistence.Entities
{
    public class Guest : Entity
    {
        public string FirstName { get; set;} 
        public string LastName { get; set;}
        public string Email { get; set;}
        public Guid EventId { get; set;}
        public string GuestType { get; set;}
        public string Organization { get; set; }
        public Guid? NationalityId { get; set;} 
        public string Tier { get; set;}
        public string InvitationStatus { get; set; }
        public DateOnly? ArrivalDate { get; set; }
        public DateOnly? DepartureDate { get; set; }
        public string FlightNumber { get; set; }
        public Guid? SeatId { get; set; }
        public string Hotel { get; set; }
        public string AccreditationStatus { get; set; }
        public Guid? InvitationTemplateId { get; set; }
        // Unguessable token embedded in the invitation email's "View invitation"
        // link so the guest can open a public (no-login) page to accept/reject.
        // Null until an invitation is actually sent.
        public Guid? InvitationToken { get; set; }
        public virtual ICollection<GuestSession> GuestSessions { get; set; } = new List<GuestSession>();
        public virtual Nationality Nationality { get; set; }
        public virtual InvitationTemplate InvitationTemplate { get; set; }
    }
}
