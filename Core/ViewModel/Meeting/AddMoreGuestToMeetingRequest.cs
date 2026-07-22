using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Meeting
{
    public class EditMeetingRequest
    {

        public Guid MeetId { get; set; }
        public Guid EventId { get; set; }
        public List<Guid>? GuestIds { get; set; }
        public string? Name { get; set; }
        public string? Location { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? Agenda { get; set; }

    }
}
