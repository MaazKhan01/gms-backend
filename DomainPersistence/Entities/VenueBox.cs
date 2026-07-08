using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueBox : Entity
    {
        public Guid? EventId { get; set; }
        public Guid VenueId { get; set; }

        public virtual ICollection<VenueLayout>? VenueLayouts { get; set; } = new List<VenueLayout>();
        public virtual ICollection<VenueBlock>? Blocks { get; set; } = new List<VenueBlock>();
        public virtual Event? Event { get; set; }
        public virtual Venue Venue { get; set; }

    }
}
