using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainPersistence.Entities
{
    public class Venue : Entity
    {
        public string Name { get; set; }
        public Guid? TypeId { get; set; }
        public List<string>? Category {  get; set; } = new List<string>();
        public string? Color { get; set; } = string.Empty;
        public virtual LookupItem? Type { get; set; }
        public virtual ICollection<VenueBox>? VenueBoxes { get; set; }
    }

}