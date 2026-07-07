using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class VenueBlock : Entity
    {
        public string Label { get; set; }
        public string Category { get; set; }
        public int Rows { get; set; }
        public int SeatsPerRow { get; set; }
    }
}
