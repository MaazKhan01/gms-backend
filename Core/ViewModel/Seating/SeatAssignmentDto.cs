using System;

namespace Core.ViewModel.Seating
{
    public class SeatAssignmentDto
    {
        public Guid SeatId { get; set; }
        /// <summary>EventGuest.PublicId of the seated participation.</summary>
        public Guid EventGuestId { get; set; }
    }
}
