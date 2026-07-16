using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Meeting
{
    public class CreateMeetingRequest
    {
        public Guid EventId { get; set; }
        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public string? Location { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? MeetingAgenda { get; set; }
        public ICollection<Guid> GuestIds { get; set; }
    }
}
