using System;
using System.Collections.Generic;

namespace Core.ViewModel.Venue
{
    public class CreateVenueRequest
    {
        public string VenueName { get; set; } = null!;
        public Guid VenueType { get; set; }
        public List<string>? Category { get; set; }
        public string Color { get; set; }
        public Guid? LocationId { get; set; }
        // Scopes the starter box (built from Blocks below) to the event/session
        // it was created under, so it actually shows up in that event's editor —
        // without these the box is a venue-wide orphan no per-event view picks up.
        public Guid? EventId { get; set; }
        public Guid? SessionId { get; set; }
        public List<CreateVenueBlockDto>? Blocks { get; set; }
    }
}
