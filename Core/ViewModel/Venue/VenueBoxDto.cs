using System;
using System.Collections.Generic;

namespace Core.ViewModel.Venue
{
    // Response DTOs (no entity references → no serialization cycles).
    public class VenueBoxDto
    {
        public Guid Id { get; set; }
        public Guid? EventId { get; set; }
        public Guid? SessionId { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public List<VenueBlockDto> Blocks { get; set; } = new();
        public List<VenueLayoutDto> VenueElements { get; set; }
    }

    public class VenueBlockDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; }
        public string? Category { get; set; }
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; }
        public int Rows { get; set; }
        public int SeatsPerRow { get; set; }
        public List<VenueLayoutPropDto> Props { get; set; } = new();
    }
}
