using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class Travel_logistics : Entity
    {
        public string BookingType { get; set;}
        public Guid GuestId { get; set; }

    }
}
