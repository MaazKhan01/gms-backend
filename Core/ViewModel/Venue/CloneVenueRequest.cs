using System;
using System.Collections.Generic;

namespace Core.ViewModel.Venue
{
    public class CloneVenueRequest
    {
        // The specific layout (box) currently open in the editor — cloned
        // without an EventId/SessionId, so it becomes the new venue's shared
        // template box (picked up automatically the first time it's assigned
        // to any event/session — see VenueHelpers.pickBox on the frontend).
        public Guid SourceBoxId { get; set; }
        public string VenueName { get; set; } = null!;
        public Guid VenueType { get; set; }
        public List<string>? Category { get; set; }
        public string? Color { get; set; }
        public Guid? LocationId { get; set; }
        public string? ImageUrl { get; set; }
    }
}
