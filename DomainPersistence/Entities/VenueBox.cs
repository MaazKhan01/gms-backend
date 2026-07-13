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
        public Guid? SessionId { get; set; }
        public Guid VenueId { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }

        public virtual ICollection<VenueLayout>? VenueLayouts { get; set; } = new List<VenueLayout>();
        public virtual ICollection<VenueBlock>? Blocks { get; set; }
        public virtual Event? Event { get; set; }
        public virtual Session? Session { get; set; }

        public virtual Venue Venue { get; set; }

    }
}
