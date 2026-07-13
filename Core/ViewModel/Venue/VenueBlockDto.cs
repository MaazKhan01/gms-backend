using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Venue
{
    public class CreateVenueBlockDto
    {
        public string Label { get; set; }
        public string? Category { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; }
        public int Rows { get; set; }
        public int SeatsPerRow { get; set; }
    }
}
