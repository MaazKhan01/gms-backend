using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueLayout : Entity
    {
        public Guid VenueBoxId { get; set; }
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; } = 0;
        public double ScaleX { get; set; } = 1;
        public double ScaleY { get; set; } = 1;
        public double OffsetX { get; set; } = 0;
        public double OffsetY { get; set; } = 0;
        public Guid? VenueBlockId { get; set; }
        public virtual VenueBlock? Block { get; set; }

        public virtual ICollection<VenueLayoutProp> VenueLayoutProps { get; set; } = new List<VenueLayoutProp>();
        public virtual VenueBox? VenueBox { get; set; }
    }
}
