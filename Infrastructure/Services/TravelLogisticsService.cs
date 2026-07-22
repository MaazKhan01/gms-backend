using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Travel_Logistics;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class TravelLogisticsService(IUnitOfWork _unitOfWork, ILogger<TravelLogisticsService> _logger) : ITravelLogistics
    {
        private static readonly string[] ValidBookingTypes =
            { BookingTypes.Flight, BookingTypes.Hotel, BookingTypes.ByRoad };

        public async Task<ApiResponse<GetBookingResponse>> CreateBookingAsync(CreateBookingDto request, Guid userId, CancellationToken ct)
        {
            try
            {
                if (request.EventId == Guid.Empty)
                    return ApiResponse<GetBookingResponse>.ErrorResponse("Event id is required.");

                if (request.GuestId == Guid.Empty)
                    return ApiResponse<GetBookingResponse>.ErrorResponse("Guest id is required.");

                if (string.IsNullOrWhiteSpace(request.BookingType) || !ValidBookingTypes.Contains(request.BookingType))
                    return ApiResponse<GetBookingResponse>.ErrorResponse(
                        $"Booking type must be one of: {string.Join(", ", ValidBookingTypes)}.");

                var guest = await _unitOfWork.Guests.Query()
                    .FirstOrDefaultAsync(g => g.Id == request.GuestId, ct);
                if (guest == null)
                    return ApiResponse<GetBookingResponse>.NotFoundResponse("Guest not found.");

                if (guest.EventId != request.EventId)
                    return ApiResponse<GetBookingResponse>.ErrorResponse("This guest does not belong to the specified event.");

                if (request.PickupLocationId is { } pickupId)
                {
                    var pickupExists = await _unitOfWork.Locations.Query().AnyAsync(l => l.Id == pickupId, ct);
                    if (!pickupExists)
                        return ApiResponse<GetBookingResponse>.NotFoundResponse("Pickup location not found.");
                }

                if (request.DropoffLocationId is { } dropoffId)
                {
                    var dropoffExists = await _unitOfWork.Locations.Query().AnyAsync(l => l.Id == dropoffId, ct);
                    if (!dropoffExists)
                        return ApiResponse<GetBookingResponse>.NotFoundResponse("Dropoff location not found.");
                }

                var booking = new Travel_logistics
                {
                    Id = Guid.NewGuid(),
                    EventId = request.EventId,
                    GuestId = request.GuestId,
                    BookingType = request.BookingType,
                    FlightNumber = request.FlightNumber,
                    FlightDate = request.FlightDate,
                    FlightDeparture = request.FlightDeparture,
                    FlightArrival = request.FlightArrival,
                    HotelId = request.HotelId,
                    HotelCheckIn = request.HotelCheckIn,
                    HotelCheckOut = request.HotelCheckOut,
                    RoomType = request.RoomType,
                    VehicleType = request.VehicleType,
                    DriverName = request.DriverName,
                    PickupLocationId = request.PickupLocationId,
                    DropoffLocationId = request.DropoffLocationId,
                };
                booking.SetCreationAudit(userId);

                await _unitOfWork.TravelLogistics.AddAsync(booking, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var response = new GetBookingResponse
                {
                    Id = booking.Id,
                    EventId = booking.EventId,
                    GuestId = booking.GuestId,
                    GuestName = $"{guest.FirstName} {guest.LastName}".Trim(),
                    BookingType = booking.BookingType,
                    FlightNumber = booking.FlightNumber,
                    FlightDate = booking.FlightDate,
                    FlightDeparture = booking.FlightDeparture,
                    FlightArrival = booking.FlightArrival,
                    HotelId = booking.HotelId,
                    HotelCheckIn = booking.HotelCheckIn,
                    HotelCheckOut = booking.HotelCheckOut,
                    RoomType = booking.RoomType,
                    VehicleType = booking.VehicleType,
                    DriverName = booking.DriverName,
                    PickupLocationId = booking.PickupLocationId,
                    DropoffLocationId = booking.DropoffLocationId,
                };

                return ApiResponse<GetBookingResponse>.SuccessResponse(response, "Booking created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking for guest {GuestId}", request.GuestId);
                return ApiResponse<GetBookingResponse>.ServerErrorResponse("An error occurred while creating the booking.");
            }
        }
    }
}
