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
