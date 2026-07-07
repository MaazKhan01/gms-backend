using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DomainPersistence.Entities
{
    public class Travel_logistics : Entity
    {
        public string BookingType { get; set; }
        public Guid GuestId { get; set; }
        public virtual Guest Guest { get; set; } = null!;
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
        public virtual Location? PickupLocation { get; set; }
        public virtual Location? DropoffLocation { get; set; }
    }
}
