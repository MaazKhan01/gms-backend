using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class Meeting : Entity
    {
        public string Name { get; set; }
        public int EventId { get; set; }
        public DateOnly Date { get; set; }
        public string? Location { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? MeetingAgenda { get; set; }
        // Attendees are per-event participations, not people: a meeting belongs to
        // one Event, so an attendee has to be that person's EventGuest row for it.
        public ICollection<EventGuest> EventGuests { get; set; } = new List<EventGuest>();

        public virtual Event Event { get; set; }

    }
}
