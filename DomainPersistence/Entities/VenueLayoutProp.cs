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
        public Guid? VenueLayoutId { get; set; }
        public Guid? VenueBlockId { get; set; }
        public string Code { get; set; }
        public string Label { get; set; }
        public int? Row { get; set; }
        public int? SeatsQuantity { get; set; }
        public List<string>? RowNames { get; set; } = [];
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
