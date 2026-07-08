using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueBlock : Entity
    {
        public Guid VenueBoxId { get; set; }
        public string Label { get; set; }
        public string? Category { get; set; }
        public int Rows { get; set; } = 1;
        public int SeatsPerRow { get; set; } = 1;
        public virtual ICollection<VenueLayout> VenueLayouts { get; set; }

        public virtual VenueBox VenueBox { get; set; }
    }
}
