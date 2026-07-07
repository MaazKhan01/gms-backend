using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueLayoutProp : Entity
    {
        public string Code { get; set; }
        public string Label { get; set; }
        public int? Row { get; set; }
        public int? Seats { get; set; }
        public List<string>? RowName { get; set; } = [];
        public decimal? PitchW { get; set; } = 0;
        public decimal? PitchH { get; set; } = 0;
        public decimal? StageW { get; set; } = 0;
        public decimal? StageH { get; set; } = 0;

    }
}
