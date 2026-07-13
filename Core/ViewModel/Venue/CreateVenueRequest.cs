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
        public List<CreateVenueBlockDto>? Blocks { get; set; }
    }
}
