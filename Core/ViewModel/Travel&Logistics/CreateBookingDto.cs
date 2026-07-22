using Core.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Travel_Logistics
{
    public class CreateBookingDto
    {
        public Guid EventId { get; set; }
        // A plain string carrying one of the Core.Constants.BookingTypes values
        // (Flight/Hotel/ByRoad) — BookingTypes itself is just a bag of string
        // constants, not a real type JSON can bind to.
        public string BookingType { get; set; }
        public Guid GuestId { get; set; }
        public string? FlightNumber { get; set; }
        public DateOnly? FlightDate { get; set; }
        public string? FlightDeparture { get; set; }
        public string? FlightArrival { get; set; }
        public Guid? HotelId { get; set; }
        public DateOnly? HotelCheckIn { get; set; }
        public DateOnly? HotelCheckOut { get; set; }
        public string? RoomType { get; set; }
        public string? VehicleType { get; set; }
        public string? DriverName { get; set; }
        public Guid? PickupLocationId { get; set; }
        public Guid? DropoffLocationId { get; set; }

    }
}
