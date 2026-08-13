using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class SeatAssign : Entity
    {
        public int SeatingId { get; set; }
        public int EventGuestId { get; set; }
        public int SeatId { get; set; }
        public virtual Seating? Seating { get; set; }
        public virtual EventGuest? EventGuest { get; set; }
        public virtual SeatProperties? Seat { get; set; }

    }
}
