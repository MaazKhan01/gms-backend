using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueLayout : Entity
    {
        public Guid VenueId { get; set; }
        public string Type { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Rotation { get; set; } = 0;
        public decimal ScaleX { get; set; } = 0;
        public decimal ScaleY { get; set; } = 0;
        public decimal OffsetX { get; set; } = 0;
        public decimal OffsetY { get; set; } = 0;
        public VenueLayoutProp VenueProps { get; set; }
        public virtual Venue Venue { get; set; }
    }
}
