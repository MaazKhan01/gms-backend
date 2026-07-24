using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class Seating : Entity
    {
        public int EventId { get; set; }
        public int VenueId { get; set; }
        public int? EventSessionId { get; set; }
        public int VenueBoxId { get; set; }
        public virtual Event Event { get; set; } = null!;
        public virtual Venue Venue { get; set; } = null!;
        public virtual Session? Session { get; set; }
        public virtual ICollection<SeatAssign>? SeatsDetail { get; set; }
        public virtual VenueBox VenueBox { get; set; }
    }
}
