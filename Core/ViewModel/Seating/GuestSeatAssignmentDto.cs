namespace Core.ViewModel.Seating
{
    // One row per seat currently held by a guest — a guest can hold more than
    // one seat across different sessions/scopes of the same event.
    public class GuestSeatAssignmentDto
    {
        public string EventTitle { get; set; }
        public string SessionTitle { get; set; } // null when the seating isn't session-scoped
        public string SeatCode { get; set; }
    }
}
