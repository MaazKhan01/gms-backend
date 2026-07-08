using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class SeatAssign : Entity
    {
        public Guid SeatingId { get; set; }
        public Guid GuestId { get; set; }
        public Guid SeatId { get; set; }
        public virtual Seating? Seating { get; set; }
        public virtual Guest? Guest { get; set; }
        public virtual SeatProperties? Seat { get; set; }

    }
}
