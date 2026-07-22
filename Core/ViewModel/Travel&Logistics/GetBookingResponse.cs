using System;

namespace Core.ViewModel.Travel_Logistics
{
    public class GetBookingResponse
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public Guid GuestId { get; set; }
        public string GuestName { get; set; }
        public string BookingType { get; set; }
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
