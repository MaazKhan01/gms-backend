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
        /// <summary>EventGuest.PublicIds. Null leaves attendees untouched; an
        /// explicit empty list clears them.</summary>
        public List<Guid>? EventGuestIds { get; set; }
        public string? Name { get; set; }
        public string? Location { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? Agenda { get; set; }

    }
}
