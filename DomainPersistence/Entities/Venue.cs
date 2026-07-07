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
        public Guid TypeId { get; set; }
        public List<string>? Category {  get; set; } = new List<string>();
        public string? Color { get; set; } = string.Empty;
        public virtual ICollection<VenueBlock>? Blocks { get; set; } = new List<VenueBlock>();
    }

}