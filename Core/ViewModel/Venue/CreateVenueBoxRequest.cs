using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Venue
{
    public class CreateVenueBoxRequest
    {
        //take current event or session id
        public Guid EventId { get; set; }
        public Guid? SessionId { get; set; }
        public Guid VenueId { get; set; }
        // Explicit canvas size (px). Null = auto-fit to the placed elements'
        // bounding box (with a sensible minimum) on the frontend.
        public int? Width { get; set; }
        public int? Height { get; set; }
        public ICollection<CreateVenueLayoutDto>? VenueLayouts { get; set; }
        public ICollection<CreateVenueBlockDto>? VenueBlocks { get; set; }

    }
}
