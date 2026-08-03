using System;

namespace Core.ViewModel.Venue
{
    public class UpdateVenueRequest
    {
        public string VenueName { get; set; } = null!;
        public Guid? LocationId { get; set; }
        public string? ImageUrl { get; set; }
    }
}
