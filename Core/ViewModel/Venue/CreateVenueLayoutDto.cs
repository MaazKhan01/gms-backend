using System.Collections.Generic;

namespace Core.ViewModel.Venue
{
    // Request-side DTOs (no Id — the server generates ids). Kept separate from the
    // Id-bearing response DTOs so an empty "id" in the payload can't break binding.
    public class CreateVenueLayoutDto
    {
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; }
        public double ScaleX { get; set; } = 1;
        public double ScaleY { get; set; } = 1;
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public List<CreateVenueLayoutPropDto>? Props { get; set; }
    }

    public class CreateVenueLayoutPropDto
    {
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
        public List<CreateSeatPropertyDto>? Seats { get; set; }
        public List<string>? RemovedSeats { get; set; }
    }

    public class CreateSeatPropertyDto
    {
        public string Code { get; set; }
        public string? Placeholder { get; set; }
        public int? Index { get; set; }
        public string? Color { get; set; }
        public string? Status { get; set; }
        public bool IsDisabled { get; set; }
        public string? SeatInfo { get; set; }
    }
}
