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
        public string? Location { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? MeetingAgenda { get; set; }
        public ICollection<Guest> GuestIds { get; set; } = new List<Guest>();

    }
}
