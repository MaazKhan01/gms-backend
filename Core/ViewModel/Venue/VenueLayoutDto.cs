using System;
using System.Collections.Generic;

namespace Core.ViewModel.Venue
{
    // A layout element (table/stage/etc.) inside a box, with its shape props and seats.
    public class VenueLayoutDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; }
        public double ScaleX { get; set; } = 1;
        public double ScaleY { get; set; } = 1;
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public List<VenueLayoutPropDto>? Props { get; set; }
    }

    public class VenueLayoutPropDto
    {
        public Guid Id { get; set; }

        public string Code { get; set; }
        public string Label { get; set; }
        public int? Row { get; set; }
        public int? SeatsQuantity { get; set; }
        public List<string>? RowNames { get; set; }
        public int? PitchW { get; set; }
        public int? PitchH { get; set; }
        public double? StageW { get; set; }
        public double? StageH { get; set; }
        public string? Color { get; set; }
        public List<SeatPropertyDto>? Seats { get; set; }
    }

    public class SeatPropertyDto
    {
        public Guid Id { get; set; }

        public string Code { get; set; }
        public int? Index { get; set; }
        public string? Color { get; set; }
        public string? Status { get; set; }
        public bool IsDisabled { get; set; }
        public string? SeatInfo { get; set; }
    }
}
