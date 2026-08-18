using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Seating
{
    public class RequestSeatAssignDto
    {
        public Guid SeatId { get; set; }
        /// <summary>EventGuest.PublicId — seating is per event, so the seat is
        /// assigned to a participation, not to a person.</summary>
        public Guid EventGuestId { get; set; }
        public Guid? EventId { get;set; }
        public Guid? SessionId { get; set; }
    }
}
