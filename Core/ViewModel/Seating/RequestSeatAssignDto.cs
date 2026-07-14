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
        public Guid GuestId { get; set; }
        public Guid? EventId { get;set; }
        public Guid? SessionId { get; set; }
    }
}
