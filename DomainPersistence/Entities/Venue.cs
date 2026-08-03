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
        public int? TypeId { get; set; }
        public List<string>? Category {  get; set; } 
        public string? Color { get; set; }
        public int? LocationId { get; set; }
        public string? ImageUrl { get; set; }
        public virtual VenueType? Type { get; set; }
        public virtual Location? Location { get; set; }
        public virtual ICollection<VenueBox>? VenueBoxes { get; set; }
    }

}