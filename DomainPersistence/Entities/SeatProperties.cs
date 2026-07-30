using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class SeatProperties : Entity
    {
        public string Code { get; set; }
        // A manager-typed display override for this seat's number/label. The
        // actual `Code` (derived from the seat's grid position) never changes
        // when this is set — it's still needed to identify the physical seat
        // (e.g. when the seat is later removed) even after its displayed
        // number has been customized.
        public string? Placeholder { get; set; }
        public int? Index { get; set; }
        public string? Color { get; set; }
        public string? Status { get; set; }
        public bool IsDisabled { get; set; } = false;
        public string? SeatInfo { get; set; }
        // VIP app session-detail seating:
        public string? Block { get; set; }
        public string? Gate { get; set; }   // e.g. "VIP Gate 3 · West Stand"
        //public Guid SeatId { get; set; } 
        //public virtual Seating Seating { get; set; }
    }
}
