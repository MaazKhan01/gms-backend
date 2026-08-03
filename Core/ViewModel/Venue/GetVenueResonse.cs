using Core.ViewModel.Travel;
using System;
using System.Collections.Generic;

namespace Core.ViewModel.Venue
{
    public class GetVenueResonse
    {
        public Guid Id { get; set; }
        public string VenueName { get; set; } = null!;
        public Guid VenueType { get; set; }
        public List<string>? Category { get; set; }
        public string Color { get; set; }
        public Guid? LocationId { get; set; }
        public LocationDto? Location { get; set; }
        public string? ImageUrl { get; set; }
        public List<VenueBoxDto>? VenueBoxes { get; set; }
    }
}
