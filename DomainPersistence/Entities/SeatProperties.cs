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
        //public Guid SeatId { get; set; } 
        //public virtual Seating Seating { get; set; }
    }
}
