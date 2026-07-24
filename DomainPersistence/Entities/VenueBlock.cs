using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueBlock : Entity
    {
        public int VenueBoxId { get; set; }
        public string Type { get; set; } = "stadium";
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; }
        public string Label { get; set; }
        public string? Category { get; set; }
        public int Rows { get; set; }
        public int SeatsPerRow { get; set; }
        public virtual ICollection<VenueLayout> VenueLayouts { get; set; }
        public virtual ICollection<VenueLayoutProp> Props { get; set; } = new List<VenueLayoutProp>();

        public virtual VenueBox VenueBox { get; set; }
    }
}
