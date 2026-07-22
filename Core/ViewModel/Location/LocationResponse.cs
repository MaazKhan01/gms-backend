using System;

namespace Core.ViewModel.Location
{
    public class LocationResponse
    {
        public Guid Id { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string? Address { get; set; }
    }
}
