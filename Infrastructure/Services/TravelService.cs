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
            .Select(x => new HotelDto { Id = x.PublicId, Name = x.Name, Address = x.Address, LocationId = x.Location.PublicId })
            .ToListAsync(ct);
        return ApiResponse<List<HotelDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<IdNameDto>>> GetVehicleTypesAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.VehicleTypes.Query()
            .OrderBy(x => x.Name)
            .Select(x => new IdNameDto { Id = x.PublicId, Name = x.Name })
            .ToListAsync(ct);
        return ApiResponse<List<IdNameDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<IdNameDto>>> GetDriversAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.DriverProfiles.Query()
            .OrderBy(x => x.User.FirstName).ThenBy(x => x.User.LastName)
            .Select(x => new IdNameDto
            {
                Id = x.PublicId,
                Name = (x.User.FirstName + " " + x.User.LastName).Trim()
            })
            .ToListAsync(ct);
        return ApiResponse<List<IdNameDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<AirportDto>>> GetAirportsAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.AirportData.Query()
            .OrderBy(x => x.Code)
            .Select(x => new AirportDto
            {
                Id = x.PublicId, Code = x.Code,
                City = x.City, Country = x.Country, Continent = x.Continent,
                LocationId = x.Location.PublicId
            })
            .ToListAsync(ct);
        return ApiResponse<List<AirportDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<LocationDto>>> GetLocationsAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Locations.Query()
            .OrderBy(x => x.Address)
            .Select(x => new LocationDto
            {
                Id = x.PublicId, Address = x.Address, Type = x.Type,
                Longitude = x.Longitude, Latitude = x.Latitude
            })
            .ToListAsync(ct);
        return ApiResponse<List<LocationDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<GuestTravelResponse>> GetGuestTravelAsync(Guid guestId, CancellationToken ct = default)
    {
        var guest = await _unitOfWork.Guests.GetByPublicIdAsync(guestId, ct);
        if (guest == null) return ApiResponse<GuestTravelResponse>.NotFoundResponse("Guest not found");

        var data = new GuestTravelResponse();

        // A guest can hold more than one of each — the wizard's single accordion
        // only ever prefills/edits the most recently added one; the rest are
        // managed from Services' per-guest booking list instead.
        var flight = await _unitOfWork.Flights.Query()
            .Include(f => f.FlightType).Include(f => f.FlightClass)
            .Include(f => f.Legs).ThenInclude(l => l.FromAirport)
            .Include(f => f.Legs).ThenInclude(l => l.ToAirport)
            .Where(f => f.GuestId == guest.Id)
            .OrderByDescending(f => f.Id)
            .FirstOrDefaultAsync(ct);
        if (flight != null)
        {
            var leg = flight.Legs.FirstOrDefault();
            data.Flight = new FlightInput
            {
                Id = flight.PublicId,
                FlightTypeId = flight.FlightType?.PublicId ?? Guid.Empty,
                FlightClassId = flight.FlightClass?.PublicId,
                Status = flight.Status,
                Seat = flight.Seat,
                FlightNumber = leg?.FlightNumber,
                FromAirportId = leg?.FromAirport?.PublicId,
                ToAirportId = leg?.ToAirport?.PublicId,
                StartTime = leg?.StartTime,
                EndTime = leg?.EndTime,
            };
        }

        var acc = await _unitOfWork.Accommodations.Query()
            .Include(a => a.Hotel).Include(a => a.RoomType)
            .Where(a => a.GuestId == guest.Id)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);
        if (acc != null)
            data.Accommodation = new AccommodationInput
            {
                Id = acc.PublicId,
                HotelId = acc.Hotel?.PublicId ?? Guid.Empty,
                RoomTypeId = acc.RoomType?.PublicId,
                CheckIn = acc.CheckIn,
                CheckOut = acc.CheckOut,
            };

        var tr = await _unitOfWork.Transports.Query()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation).Include(t => t.VehicleType)
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.GuestId == guest.Id, ct);
        if (tr != null)
            data.Transport = new TransportInput
            {
                Id = tr.PublicId,
                PickupLocationId = tr.PickupLocation?.PublicId,
                DropoffLocationId = tr.DropoffLocation?.PublicId,
                VehicleTypeId = tr.VehicleType?.PublicId,
                DriverId = tr.Driver?.PublicId,
                TripStatus = tr.TripStatus,
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
                Id = f.PublicId,
                GuestId = f.Guest.PublicId,
                GuestName = (f.Guest.FirstName + " " + f.Guest.LastName).Trim(),
                Organization = f.Guest.Organization,
                Tier = f.Guest.Tier,
                Status = f.Status,
                FlightType = f.FlightType.Name,
                FlightClass = f.FlightClass.Name,
                Seat = f.Seat,
                LegCount = f.Legs.Count,
                Legs = f.Legs
                    .OrderBy(l => l.StartTime)
                    .Select(l => new FlightLegRow
                    {
                        Id = l.PublicId,
                        FlightNumber = l.FlightNumber,
                        DepartureCode = l.FromAirport.Code,
                        DepartureCity = l.FromAirport.City,
                        DepartureCountry = l.FromAirport.Country,
                        ArrivalCode = l.ToAirport.Code,
                        ArrivalCity = l.ToAirport.City,
                        ArrivalCountry = l.ToAirport.Country,
                        StartTime = l.StartTime,
                        EndTime = l.EndTime,
                    }).ToList(),
            })
            .ToListAsync(ct);

        // Summary fields come from the itinerary ends: depart on the first leg, land on the last.
        foreach (var row in data)
        {
            var first = row.Legs.FirstOrDefault();
            var last = row.Legs.LastOrDefault();
            if (first == null) continue;

            row.FlightNumber = first.FlightNumber;
            row.DepartureCode = first.DepartureCode;
            row.DepartureCity = first.DepartureCity;
            row.Date = first.StartTime;
            row.ArrivalCode = last.ArrivalCode;
            row.ArrivalCity = last.ArrivalCity;
            row.ArrivalTime = last.EndTime;
        }

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
                Id = a.PublicId,
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
                Id = t.PublicId,
                GuestId = t.Guest.PublicId,
                GuestName = (t.Guest.FirstName + " " + t.Guest.LastName).Trim(),
                Organization = t.Guest.Organization,
                Tier = t.Guest.Tier,
                VehicleType = t.VehicleType.Name,
                DriverId = t.Driver == null ? null : (Guid?)t.Driver.PublicId,
                DriverName = t.Driver == null ? null : (t.Driver.User.FirstName + " " + t.Driver.User.LastName).Trim(),
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

            // A guest can hold more than one flight/hotel/transport booking. Each
            // Input carries the specific booking's Id when it's editing one that
            // already exists (the wizard's prefill, or a per-booking edit) — that
            // updates the record in place. No Id (Services' "New Booking") always
            // adds a new one alongside whatever the guest already has.
            if (request.Flight != null)
            {
                var typeId = await ResolveId(_unitOfWork.FlightTypes, request.Flight.FlightTypeId, ct);
                if (typeId == null) return ApiResponse<bool>.ErrorResponse("Invalid flight type");
                var classId = await ResolveNullableId(_unitOfWork.FlightClasses, request.Flight.FlightClassId, ct);
                var fromAirportId = await ResolveNullableId(_unitOfWork.AirportData, request.Flight.FromAirportId, ct);
                var toAirportId = await ResolveNullableId(_unitOfWork.AirportData, request.Flight.ToAirportId, ct);

                Flight flight = null;
                if (request.Flight.Id is { } flightId && flightId != Guid.Empty)
                    flight = await _unitOfWork.Flights.Query().Include(f => f.Legs)
                        .FirstOrDefaultAsync(f => f.PublicId == flightId && f.GuestId == guest.Id, ct);

                var isNewFlight = flight == null;
                if (isNewFlight)
                {
                    flight = new Flight { GuestId = guest.Id, Legs = new List<FlightLeg> { new() } };
                }

                flight.FlightTypeId = typeId.Value;
                flight.FlightClassId = classId;
                flight.Status = request.Flight.Status;
                flight.Seat = request.Flight.Seat;

                var leg = flight.Legs.First();
                leg.FlightNumber = request.Flight.FlightNumber;
                leg.FromAirportId = fromAirportId;
                leg.ToAirportId = toAirportId;
                leg.StartTime = request.Flight.StartTime;
                leg.EndTime = request.Flight.EndTime;

                if (isNewFlight) await _unitOfWork.Flights.AddAsync(flight, ct);
            }

            if (request.Accommodation != null)
            {
                var hotelId = await ResolveId(_unitOfWork.AccommodationHotels, request.Accommodation.HotelId, ct);
                if (hotelId == null) return ApiResponse<bool>.ErrorResponse("Invalid hotel");
                var roomTypeId = await ResolveNullableId(_unitOfWork.AccommodationRoomTypes, request.Accommodation.RoomTypeId, ct);

                Accommodation acc = null;
                if (request.Accommodation.Id is { } accId && accId != Guid.Empty)
                    acc = await _unitOfWork.Accommodations.Query()
                        .FirstOrDefaultAsync(a => a.PublicId == accId && a.GuestId == guest.Id, ct);

                var isNewAcc = acc == null;
                if (isNewAcc) acc = new Accommodation { GuestId = guest.Id };

                acc.AccommodationHotelId = hotelId.Value;
                acc.RoomTypeId = roomTypeId;
                acc.CheckIn = request.Accommodation.CheckIn;
                acc.CheckOut = request.Accommodation.CheckOut;
                acc.RoomView = request.Accommodation.RoomView;
                acc.GuestCount = request.Accommodation.GuestCount;
                acc.ConciergeName = request.Accommodation.ConciergeName;
                acc.ConciergePhone = request.Accommodation.ConciergePhone;

                await _unitOfWork.Accommodations.AddAsync(new Accommodation
                {
                    GuestId = guest.Id,
                    AccommodationHotelId = hotelId.Value,
                    RoomTypeId = roomTypeId,
                    CheckIn = request.Accommodation.CheckIn,
                    CheckOut = request.Accommodation.CheckOut,
                }, ct);
                    GuestCount = request.Accommodation.GuestCount,
                    ConciergeName = request.Accommodation.ConciergeName,
                    ConciergePhone = request.Accommodation.ConciergePhone,
                }, ct);
            }

            if (request.Transport != null)
            {
                var pickupId = await ResolveNullableId(_unitOfWork.Locations, request.Transport.PickupLocationId, ct);
                var dropoffId = await ResolveNullableId(_unitOfWork.Locations, request.Transport.DropoffLocationId, ct);
                var vehicleTypeId = await ResolveNullableId(_unitOfWork.VehicleTypes, request.Transport.VehicleTypeId, ct);
                var driverId = await ResolveNullableId(_unitOfWork.DriverProfiles, request.Transport.DriverId, ct);
                if (request.Transport.DriverId is { } d && d != Guid.Empty && driverId == null)
                    return ApiResponse<bool>.ErrorResponse("Invalid driver");

                Transport tr = null;
                if (request.Transport.Id is { } trId && trId != Guid.Empty)
                    tr = await _unitOfWork.Transports.Query()
                        .FirstOrDefaultAsync(t => t.PublicId == trId && t.GuestId == guest.Id, ct);

                var isNewTransport = tr == null;
                if (isNewTransport) tr = new Transport { GuestId = guest.Id };

                tr.PickupLocationId = pickupId;
                tr.DropoffLocationId = dropoffId;
                tr.VehicleTypeId = vehicleTypeId;
                tr.Plate = request.Transport.Plate;
                tr.TripStatus = request.Transport.TripStatus;
                tr.DriverName = request.Transport.DriverName;
                tr.DriverPhone = request.Transport.DriverPhone;
                await _unitOfWork.Transports.AddAsync(new Transport
                {
                    GuestId = guest.Id,
                    PickupLocationId = pickupId,
                    DropoffLocationId = dropoffId,
                    VehicleTypeId = vehicleTypeId,
                    DriverId = driverId,
                    TripStatus = request.Transport.TripStatus,
                    PickupTime = request.Transport.PickupTime,
                    EstimatedArrival = request.Transport.EstimatedArrival,
                }, ct);
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

    // ── Remove one specific booking (a guest may have several of a kind) ────────
    public async Task<ApiResponse<bool>> DeleteFlightAsync(Guid id, CancellationToken ct = default)
    {
        var flight = await _unitOfWork.Flights.GetByPublicIdAsync(id, ct);
        if (flight == null) return ApiResponse<bool>.NotFoundResponse("Flight booking not found");
        _unitOfWork.Flights.Remove(flight);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, "Flight booking removed");
    }

    public async Task<ApiResponse<bool>> DeleteAccommodationAsync(Guid id, CancellationToken ct = default)
    {
        var acc = await _unitOfWork.Accommodations.GetByPublicIdAsync(id, ct);
        if (acc == null) return ApiResponse<bool>.NotFoundResponse("Accommodation booking not found");
        _unitOfWork.Accommodations.Remove(acc);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, "Accommodation booking removed");
    }

    public async Task<ApiResponse<bool>> DeleteTransportAsync(Guid id, CancellationToken ct = default)
    {
        var tr = await _unitOfWork.Transports.GetByPublicIdAsync(id, ct);
        if (tr == null) return ApiResponse<bool>.NotFoundResponse("Transport booking not found");
        _unitOfWork.Transports.Remove(tr);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, "Transport booking removed");
    }

    // ── Lookup record creation ───────────────────────────────────────────────
    public Task<ApiResponse<IdNameDto>> CreateFlightTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default)
        => CreateNamedAsync(_unitOfWork.FlightTypes, request.Name, userId, n => new FlightType { Name = n }, ct);

    public Task<ApiResponse<IdNameDto>> CreateFlightClassAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default)
        => CreateNamedAsync(_unitOfWork.FlightClasses, request.Name, userId, n => new FlightClass { Name = n }, ct);

    public Task<ApiResponse<IdNameDto>> CreateRoomTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default)
        => CreateNamedAsync(_unitOfWork.AccommodationRoomTypes, request.Name, userId, n => new AccommodationRoomType { Name = n }, ct);

    public Task<ApiResponse<IdNameDto>> CreateVehicleTypeAsync(CreateNamedLookupRequest request, int userId, CancellationToken ct = default)
        => CreateNamedAsync(_unitOfWork.VehicleTypes, request.Name, userId, n => new VehicleType { Name = n }, ct);

    public async Task<ApiResponse<AirportDto>> CreateAirportAsync(CreateAirportRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return ApiResponse<AirportDto>.ErrorResponse("Code is required");

            var code = request.Code.Trim().ToUpperInvariant();
            if (await _unitOfWork.AirportData.AnyAsync(a => a.Code == code, ct))
                return ApiResponse<AirportDto>.ErrorResponse("Airport code already exists");

            var airport = new AirportData
            {
                Code = code,
                City = request.City?.Trim(),
                Country = request.Country?.Trim(),
                Continent = request.Continent?.Trim(),
                LocationId = await ResolveNullableId(_unitOfWork.Locations, request.LocationId, ct)
            };
            airport.SetCreationAudit(userId);
            await _unitOfWork.AirportData.AddAsync(airport, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<AirportDto>.SuccessResponse(
                new AirportDto
                {
                    Id = airport.PublicId, Code = airport.Code,
                    City = airport.City, Country = airport.Country, Continent = airport.Continent,
                    LocationId = request.LocationId
                }, "Airport created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating airport");
            return ApiResponse<AirportDto>.ServerErrorResponse("An error occurred while creating the airport");
        }
    }

    public async Task<ApiResponse<LocationDto>> CreateLocationAsync(LocationRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Longitude) || string.IsNullOrWhiteSpace(request.Latitude))
                return ApiResponse<LocationDto>.ErrorResponse("Longitude and latitude are required");

            var location = new Location
            {
                Longitude = request.Longitude.Trim(),
                Latitude = request.Latitude.Trim(),
                Address = request.Address?.Trim(),
                Type = request.Type?.Trim()
            };
            location.SetCreationAudit(userId);
            await _unitOfWork.Locations.AddAsync(location, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<LocationDto>.SuccessResponse(
                new LocationDto
                {
                    Id = location.PublicId, Address = location.Address, Type = location.Type,
                    Longitude = location.Longitude, Latitude = location.Latitude
                }, "Location created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return ApiResponse<LocationDto>.ServerErrorResponse("An error occurred while creating the location");
        }
    }

    public async Task<ApiResponse<LocationDto>> UpdateLocationAsync(Guid id, LocationRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Longitude) || string.IsNullOrWhiteSpace(request.Latitude))
                return ApiResponse<LocationDto>.ErrorResponse("Longitude and latitude are required");

            var location = await _unitOfWork.Locations.GetByPublicIdAsync(id, ct);
            if (location == null) return ApiResponse<LocationDto>.NotFoundResponse("Location not found");

            location.Longitude = request.Longitude.Trim();
            location.Latitude = request.Latitude.Trim();
            location.Address = request.Address?.Trim();
            location.Type = request.Type?.Trim();
            location.SetUpdateAudit(userId);

            _unitOfWork.Locations.Update(location);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<LocationDto>.SuccessResponse(
                new LocationDto
                {
                    Id = location.PublicId, Address = location.Address, Type = location.Type,
                    Longitude = location.Longitude, Latitude = location.Latitude
                }, "Location updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {LocationId}", id);
            return ApiResponse<LocationDto>.ServerErrorResponse("An error occurred while updating the location");
        }
    }

    public async Task<ApiResponse<HotelDto>> CreateHotelAsync(CreateHotelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<HotelDto>.ErrorResponse("Name is required");

            var hotel = new AccommodationHotel
            {
                Name = request.Name.Trim(),
                Address = request.Address?.Trim() ?? string.Empty,
                LocationId = await ResolveNullableId(_unitOfWork.Locations, request.LocationId, ct)
            };
            hotel.SetCreationAudit(userId);
            await _unitOfWork.AccommodationHotels.AddAsync(hotel, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<HotelDto>.SuccessResponse(
                new HotelDto { Id = hotel.PublicId, Name = hotel.Name, Address = hotel.Address, LocationId = request.LocationId }, "Hotel created");
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
