using System;

namespace Core.ViewModel.Seating
{
    public class SeatAssignmentDto
    {
        public Guid SeatId { get; set; }
        public Guid GuestId { get; set; }
    }
}
