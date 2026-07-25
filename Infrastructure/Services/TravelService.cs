using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Travel;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Per-guest travel: flight / accommodation / transport. Each is optional — the
// admin picks which ones a guest gets. Ids in/out are public Guids.
public class TravelService(IUnitOfWork _unitOfWork, ILogger<TravelService> _logger) : ITravelService
{
    public async Task<ApiResponse<List<IdNameDto>>> GetFlightTypesAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.FlightTypes.Query()
            .OrderBy(x => x.Name)
            .Select(x => new IdNameDto { Id = x.PublicId, Name = x.Name })
            .ToListAsync(ct);
        return ApiResponse<List<IdNameDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<IdNameDto>>> GetFlightClassesAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.FlightClasses.Query()
            .OrderBy(x => x.Name)
            .Select(x => new IdNameDto { Id = x.PublicId, Name = x.Name })
            .ToListAsync(ct);
        return ApiResponse<List<IdNameDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<IdNameDto>>> GetRoomTypesAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.AccommodationRoomTypes.Query()
            .OrderBy(x => x.Name)
            .Select(x => new IdNameDto { Id = x.PublicId, Name = x.Name })
            .ToListAsync(ct);
        return ApiResponse<List<IdNameDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<HotelDto>>> GetHotelsAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.AccommodationHotels.Query()
            .OrderBy(x => x.Name)
            .Select(x => new HotelDto { Id = x.PublicId, Name = x.Name, Address = x.Address })
            .ToListAsync(ct);
        return ApiResponse<List<HotelDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<LocationDto>>> GetLocationsAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Locations.Query()
            .OrderBy(x => x.Address)
            .Select(x => new LocationDto { Id = x.PublicId, Address = x.Address })
            .ToListAsync(ct);
        return ApiResponse<List<LocationDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<GuestTravelResponse>> GetGuestTravelAsync(Guid guestId, CancellationToken ct = default)
    {
        var guest = await _unitOfWork.Guests.GetByPublicIdAsync(guestId, ct);
        if (guest == null) return ApiResponse<GuestTravelResponse>.NotFoundResponse("Guest not found");

        var data = new GuestTravelResponse();

        var flight = await _unitOfWork.Flights.Query()
            .Include(f => f.FlightType).Include(f => f.FlightClass).Include(f => f.Legs)
            .FirstOrDefaultAsync(f => f.GuestId == guest.Id, ct);
        if (flight != null)
        {
            var leg = flight.Legs.FirstOrDefault();
            data.Flight = new FlightInput
            {
                FlightTypeId = flight.FlightType?.PublicId ?? Guid.Empty,
                FlightClassId = flight.FlightClass?.PublicId,
                Status = flight.Status,
                Seat = flight.Seat,
                FlightNumber = leg?.FlightNumber,
                DepartureCode = leg?.DepartureCode,
                DepartureCity = leg?.DepartureCity,
                ArrivalCode = leg?.ArrivalCode,
                ArrivalCity = leg?.ArrivalCity,
                StartTime = leg?.StartTime,
                EndTime = leg?.EndTime,
            };
        }

        var acc = await _unitOfWork.Accommodations.Query()
            .Include(a => a.Hotel).Include(a => a.RoomType)
            .FirstOrDefaultAsync(a => a.GuestId == guest.Id, ct);
        if (acc != null)
            data.Accommodation = new AccommodationInput
            {
                HotelId = acc.Hotel?.PublicId ?? Guid.Empty,
                RoomTypeId = acc.RoomType?.PublicId,
                CheckIn = acc.CheckIn,
                CheckOut = acc.CheckOut,
                RoomView = acc.RoomView,
                GuestCount = acc.GuestCount,
                ConciergeName = acc.ConciergeName,
                ConciergePhone = acc.ConciergePhone,
            };

        var tr = await _unitOfWork.Transports.Query()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation)
            .FirstOrDefaultAsync(t => t.GuestId == guest.Id, ct);
        if (tr != null)
            data.Transport = new TransportInput
            {
                PickupLocationId = tr.PickupLocation?.PublicId,
                DropoffLocationId = tr.DropoffLocation?.PublicId,
                VehicleType = tr.VehicleType,
                Plate = tr.Plate,
                TripStatus = tr.TripStatus,
                DriverName = tr.DriverName,
                DriverPhone = tr.DriverPhone,
                DriverRating = tr.DriverRating,
                PickupTime = tr.PickupTime,
                EstimatedArrival = tr.EstimatedArrival,
            };

        return ApiResponse<GuestTravelResponse>.SuccessResponse(data);
    }

    // ── Per-event booking lists (admin travel tabs) ──────────────────────────
    public async Task<ApiResponse<List<EventFlightRow>>> GetEventFlightsAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<List<EventFlightRow>>.NotFoundResponse("Event not found");

        var data = await _unitOfWork.Flights.Query()
            .Where(f => f.Guest.EventId == ev.Id)
            .OrderBy(f => f.Guest.FirstName).ThenBy(f => f.Guest.LastName)
            .Select(f => new EventFlightRow
            {
                GuestId = f.Guest.PublicId,
                GuestName = (f.Guest.FirstName + " " + f.Guest.LastName).Trim(),
                Organization = f.Guest.Organization,
                Tier = f.Guest.Tier,
                Status = f.Status,
                FlightNumber = f.Legs.Select(l => l.FlightNumber).FirstOrDefault(),
                DepartureCode = f.Legs.Select(l => l.DepartureCode).FirstOrDefault(),
                ArrivalCode = f.Legs.Select(l => l.ArrivalCode).FirstOrDefault(),
                Date = f.Legs.Select(l => l.StartTime).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return ApiResponse<List<EventFlightRow>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<EventAccommodationRow>>> GetEventAccommodationsAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<List<EventAccommodationRow>>.NotFoundResponse("Event not found");

        var data = await _unitOfWork.Accommodations.Query()
            .Where(a => a.Guest.EventId == ev.Id)
            .OrderBy(a => a.Guest.FirstName).ThenBy(a => a.Guest.LastName)
            .Select(a => new EventAccommodationRow
            {
                GuestId = a.Guest.PublicId,
                GuestName = (a.Guest.FirstName + " " + a.Guest.LastName).Trim(),
                Organization = a.Guest.Organization,
                Tier = a.Guest.Tier,
                Hotel = a.Hotel.Name,
                RoomType = a.RoomType.Name,
                CheckIn = a.CheckIn,
                CheckOut = a.CheckOut,
            })
            .ToListAsync(ct);
        return ApiResponse<List<EventAccommodationRow>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<EventTransportRow>>> GetEventTransportsAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<List<EventTransportRow>>.NotFoundResponse("Event not found");

        var data = await _unitOfWork.Transports.Query()
            .Where(t => t.Guest.EventId == ev.Id)
            .OrderBy(t => t.Guest.FirstName).ThenBy(t => t.Guest.LastName)
            .Select(t => new EventTransportRow
            {
                GuestId = t.Guest.PublicId,
                GuestName = (t.Guest.FirstName + " " + t.Guest.LastName).Trim(),
                Organization = t.Guest.Organization,
                Tier = t.Guest.Tier,
                VehicleType = t.VehicleType,
                DriverName = t.DriverName,
                Pickup = t.PickupLocation.Address,
                Dropoff = t.DropoffLocation.Address,
                PickupTime = t.PickupTime,
                TripStatus = t.TripStatus,
            })
            .ToListAsync(ct);
        return ApiResponse<List<EventTransportRow>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<bool>> SaveGuestTravelAsync(Guid guestId, GuestTravelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var guest = await _unitOfWork.Guests.GetByPublicIdAsync(guestId, ct);
            if (guest == null) return ApiResponse<bool>.NotFoundResponse("Guest not found");

            if (request.Flight != null)
            {
                var typeId = await ResolveId(_unitOfWork.FlightTypes, request.Flight.FlightTypeId, ct);
                if (typeId == null) return ApiResponse<bool>.ErrorResponse("Invalid flight type");
                var classId = await ResolveNullableId(_unitOfWork.FlightClasses, request.Flight.FlightClassId, ct);

                var existing = await _unitOfWork.Flights.FindAsync(f => f.GuestId == guest.Id, ct);
                _unitOfWork.Flights.RemoveRange(existing);

                var flight = new Flight
                {
                    GuestId = guest.Id,
                    FlightTypeId = typeId.Value,
                    FlightClassId = classId,
                    Status = request.Flight.Status,
                    Seat = request.Flight.Seat,
                    Legs = new List<FlightLeg>
                    {
                        new()
                        {
                            FlightNumber = request.Flight.FlightNumber,
                            DepartureCode = request.Flight.DepartureCode,
                            DepartureCity = request.Flight.DepartureCity,
                            ArrivalCode = request.Flight.ArrivalCode,
                            ArrivalCity = request.Flight.ArrivalCity,
                            StartTime = request.Flight.StartTime,
                            EndTime = request.Flight.EndTime,
                        }
                    }
                };
                await _unitOfWork.Flights.AddAsync(flight, ct);
            }

            if (request.Accommodation != null)
            {
                var hotelId = await ResolveId(_unitOfWork.AccommodationHotels, request.Accommodation.HotelId, ct);
                if (hotelId == null) return ApiResponse<bool>.ErrorResponse("Invalid hotel");
                var roomTypeId = await ResolveNullableId(_unitOfWork.AccommodationRoomTypes, request.Accommodation.RoomTypeId, ct);

                var existing = await _unitOfWork.Accommodations.FindAsync(a => a.GuestId == guest.Id, ct);
                _unitOfWork.Accommodations.RemoveRange(existing);

                await _unitOfWork.Accommodations.AddAsync(new Accommodation
                {
                    GuestId = guest.Id,
                    AccommodationHotelId = hotelId.Value,
                    RoomTypeId = roomTypeId,
                    CheckIn = request.Accommodation.CheckIn,
                    CheckOut = request.Accommodation.CheckOut,
                    RoomView = request.Accommodation.RoomView,
                    GuestCount = request.Accommodation.GuestCount,
                    ConciergeName = request.Accommodation.ConciergeName,
                    ConciergePhone = request.Accommodation.ConciergePhone,
                }, ct);
            }

            if (request.Transport != null)
            {
                var pickupId = await ResolveNullableId(_unitOfWork.Locations, request.Transport.PickupLocationId, ct);
                var dropoffId = await ResolveNullableId(_unitOfWork.Locations, request.Transport.DropoffLocationId, ct);

                var existing = await _unitOfWork.Transports.FindAsync(t => t.GuestId == guest.Id, ct);
                _unitOfWork.Transports.RemoveRange(existing);

                await _unitOfWork.Transports.AddAsync(new Transport
                {
                    GuestId = guest.Id,
                    PickupLocationId = pickupId,
                    DropoffLocationId = dropoffId,
                    VehicleType = request.Transport.VehicleType,
                    Plate = request.Transport.Plate,
                    TripStatus = request.Transport.TripStatus,
                    DriverName = request.Transport.DriverName,
                    DriverPhone = request.Transport.DriverPhone,
                    DriverRating = request.Transport.DriverRating,
                    PickupTime = request.Transport.PickupTime,
                    EstimatedArrival = request.Transport.EstimatedArrival,
                }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, "Travel saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving guest travel {GuestId}", guestId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while saving travel");
        }
    }

    private static async Task<int?> ResolveId<T>(IGenericRepository<T> repo, Guid publicId, CancellationToken ct) where T : Entity
        => (await repo.GetByPublicIdAsync(publicId, ct))?.Id;

    private static async Task<int?> ResolveNullableId<T>(IGenericRepository<T> repo, Guid? publicId, CancellationToken ct) where T : Entity
        => publicId == null || publicId == Guid.Empty ? null : (await repo.GetByPublicIdAsync(publicId.Value, ct))?.Id;

    // ── Lookup record creation ───────────────────────────────────────────────
    public Task<ApiResponse<IdNameDto>> CreateFlightTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default)
        => CreateNamedAsync(_unitOfWork.FlightTypes, request.Name, userId, n => new FlightType { Name = n }, ct);

    public Task<ApiResponse<IdNameDto>> CreateFlightClassAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default)
        => CreateNamedAsync(_unitOfWork.FlightClasses, request.Name, userId, n => new FlightClass { Name = n }, ct);

    public Task<ApiResponse<IdNameDto>> CreateRoomTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default)
        => CreateNamedAsync(_unitOfWork.AccommodationRoomTypes, request.Name, userId, n => new AccommodationRoomType { Name = n }, ct);

    public async Task<ApiResponse<HotelDto>> CreateHotelAsync(CreateHotelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<HotelDto>.ErrorResponse("Name is required");

            var hotel = new AccommodationHotel { Name = request.Name.Trim(), Address = request.Address?.Trim() ?? string.Empty };
            hotel.SetCreationAudit(userId);
            await _unitOfWork.AccommodationHotels.AddAsync(hotel, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<HotelDto>.SuccessResponse(
                new HotelDto { Id = hotel.PublicId, Name = hotel.Name, Address = hotel.Address }, "Hotel created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hotel");
            return ApiResponse<HotelDto>.ServerErrorResponse("An error occurred while creating the hotel");
        }
    }

    // Shared create for the name-only lookups (flight type / class / room type).
    private async Task<ApiResponse<IdNameDto>> CreateNamedAsync<T>(
        IGenericRepository<T> repo, string name, int userId, Func<string, T> make, CancellationToken ct) where T : Entity
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return ApiResponse<IdNameDto>.ErrorResponse("Name is required");

            var entity = make(name.Trim());
            entity.SetCreationAudit(userId);
            await repo.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<IdNameDto>.SuccessResponse(
                new IdNameDto { Id = entity.PublicId, Name = name.Trim() }, "Created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating {Type}", typeof(T).Name);
            return ApiResponse<IdNameDto>.ServerErrorResponse("An error occurred while creating the record");
        }
    }
}
