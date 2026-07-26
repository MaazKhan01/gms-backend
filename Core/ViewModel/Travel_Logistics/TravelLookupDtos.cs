using System;

namespace Core.ViewModel.Travel_Logistics
{
    public class CreateNamedLookupDto
    {
        public string Name { get; set; }
    }

    public class CreateHotelDto
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }

    public class HotelLookupResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
    }

    public class LocationLookupResponse
    {
        public Guid Id { get; set; }
        public string Address { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
    }
}
