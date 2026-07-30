using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueLayoutProp : Entity
    {
        // A prop belongs to EITHER a layout element or a block (stadium block).
        public int? VenueLayoutId { get; set; }
        public int? VenueBlockId { get; set; }
        public string Code { get; set; }
        public string Label { get; set; }
        public int? Row { get; set; }
        public int? SeatsQuantity { get; set; }
        public List<string>? RowNames { get; set; } = [];
        // Codes (e.g. "C8") of seats hidden from this prop's grid — kept
        // separate from the Seats collection itself: a removed seat simply has
        // no SeatProperties row, so on load this is what tells the editor to
        // render blank space at that grid position instead of a seat.
        public List<string>? RemovedSeats { get; set; } = [];
        public int? PitchW { get; set; } = 0;
        public int? PitchH { get; set; } = 0;
        public double? StageW { get; set; } = 0;
        public double? StageH { get; set; } = 0;
        public string? Color { get; set; }
        public virtual List<SeatProperties>? Seats { get; set; }

        public virtual VenueLayout? Layout { get; set; }
        public virtual VenueBlock? Block { get; set; }

    }
}
