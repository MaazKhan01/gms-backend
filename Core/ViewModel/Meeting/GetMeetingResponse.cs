using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Meeting
{
    public class GetMeetingResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public string? Location { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? MeetingAgenda { get; set; }
        public List<GuestInfo>? Guests { get; set; }
        public Guid EventId { get; set; }
    }
    public class GuestInfo
    {
        public string? Name { get; set; }
        public Guid? Id { get; set; }
    }
}
