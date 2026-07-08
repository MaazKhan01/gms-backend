using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueLayoutProp : Entity
    {
        public Guid VenueLayoutId { get; set; }
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

        public virtual VenueLayout Layout { get; set; }

    }
}
