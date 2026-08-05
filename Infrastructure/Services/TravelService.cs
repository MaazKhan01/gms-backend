using Core.Constants;
using Core.Constants.Notification;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Travel;
using DomainPersistence.Entities;
using DomainPersistence.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Per-guest travel: flight / accommodation / transport. Each is optional — the
// admin picks which ones a guest gets. Ids in/out are public Guids.
public class TravelService(
    IUnitOfWork _unitOfWork,
    INotificationManagerService _notifications,
    ITransportationConflictValidator _conflictValidator,
    IAccommodationInventoryService _inventory,
    ILogger<TravelService> _logger) : ITravelService
{
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
            .Select(x => new HotelDto { Id = x.PublicId, Name = x.Name, Address = x.Address, ImageUrl = x.ImageUrl, LocationId = x.Location.PublicId })
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

    // Sourced from Users (not just DriverProfiles) so a driver who's been
    // deactivated or reassigned to a different role drops out of the dropdown
    // immediately, even though their DriverProfile row is left in place.
    //
    // Fixed drivers only: an Open driver isn't dispatch's to assign — they claim
    // guest requests themselves from the driver app's job pool.
    public async Task<ApiResponse<List<IdNameDto>>> GetDriversAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Users.Query()
            .Where(u => u.IsActive && u.Role.Code == Roles.DRIVER && u.DriverProfile != null
                     && u.DriverProfile.DriverType == DriverType.Fixed)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new IdNameDto
            {
                Id = u.DriverProfile.PublicId,
                Name = (u.FirstName + " " + u.LastName).Trim()
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

    public async Task<ApiResponse<GuestTravelResponse>> GetGuestTravelAsync(
        Guid guestId, Guid? bookingId = null, CancellationToken ct = default)
    {
        var guest = await _unitOfWork.Guests.GetByPublicIdAsync(guestId, ct);
        if (guest == null) return ApiResponse<GuestTravelResponse>.NotFoundResponse("Guest not found");

        var data = new GuestTravelResponse
        {
            AllowTransportRequest = GuestServices.Allows(guest.AllowedServicesJson, GuestServiceType.Transport),
        };

        // A guest can hold more than one of each. With no bookingId the wizard's
        // single accordion prefills the most recently added one of each kind;
        // with a bookingId (Services' per-booking Edit) exactly that booking is
        // returned and the other two sections come back empty — a booking id only
        // ever matches one kind, and the edit form only reads its own section.
        var flight = await _unitOfWork.Flights.Query()
            .Include(f => f.FlightClass)
            .Include(f => f.Legs).ThenInclude(l => l.FromAirport)
            .Include(f => f.Legs).ThenInclude(l => l.ToAirport)
            .Include(f => f.Legs).ThenInclude(l => l.FlightClass)
            .Where(f => f.GuestId == guest.Id && (bookingId == null || f.PublicId == bookingId))
            .OrderByDescending(f => f.Id)
            .FirstOrDefaultAsync(ct);
        if (flight != null)
        {
            data.Flight = new FlightInput
            {
                Id = flight.PublicId,
                FlightType = FlightTypeCode(flight.FlightType),
                FlightClassId = flight.FlightClass?.PublicId,
                Status = flight.Status,
                Seat = flight.Seat,
                DepartureTime = flight.DepartureTime,
                ArrivalTime = flight.ArrivalTime,
                Legs = flight.Legs
                    .OrderBy(l => l.StartTime).ThenBy(l => l.Id)
                    .Select(l => new FlightLegInput
                    {
                        Id = l.PublicId,
                        FlightNumber = l.FlightNumber,
                        FromAirportId = l.FromAirport?.PublicId,
                        ToAirportId = l.ToAirport?.PublicId,
                        StartTime = l.StartTime,
                        EndTime = l.EndTime,
                        FlightClassId = l.FlightClass?.PublicId,
                        Seat = l.Seat,
                    }).ToList(),
            };
        }

        var acc = await _unitOfWork.Accommodations.Query()
            .Include(a => a.Hotel).Include(a => a.RoomType)
            .Where(a => a.GuestId == guest.Id && (bookingId == null || a.PublicId == bookingId))
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
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation).Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Where(t => t.GuestId == guest.Id && (bookingId == null || t.PublicId == bookingId))
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);
        if (tr != null)
            data.Transport = new TransportInput
            {
                Id = tr.PublicId,
                PickupLocationId = tr.PickupLocation?.PublicId,
                DropoffLocationId = tr.DropoffLocation?.PublicId,
                VehicleId = tr.Vehicle?.PublicId,
                DriverId = tr.Driver?.PublicId,
                TripStatus = tr.TripStatus,
                PickupTime = tr.PickupTime,
                DropoffTime = tr.DropoffTime,
                ActualPickupTime = tr.ActualPickupTime,
                ActualDropOffTime = tr.ActualDropOffTime,
            };

        return ApiResponse<GuestTravelResponse>.SuccessResponse(data);
    }

    // ── Per-event booking lists (admin travel tabs) ──────────────────────────
    public async Task<ApiResponse<PaginatedResponse<EventFlightRow>>> GetEventFlightsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<PaginatedResponse<EventFlightRow>>.NotFoundResponse("Event not found");

        var query = _unitOfWork.Flights.Query().Where(f => f.Guest.EventId == ev.Id);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(f =>
                (f.Guest.FirstName + " " + f.Guest.LastName).Contains(term) ||
                f.Legs.Any(l => l.FlightNumber.Contains(term)));
        }

        var total = await query.CountAsync(ct);

        // FlightType is an enum column — projected raw and turned into its code
        // in memory, since EF can't translate the mapping.
        var rawRows = await query
            .OrderBy(f => f.Guest.FirstName).ThenBy(f => f.Guest.LastName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(f => new { Type = f.FlightType, Row = new EventFlightRow
            {
                Id = f.PublicId,
                GuestId = f.Guest.PublicId,
                GuestName = (f.Guest.FirstName + " " + f.Guest.LastName).Trim(),
                PhotoUrl = f.Guest.PhotoUrl,
                Organization = f.Guest.Organization,
                Tier = f.Guest.Tier,
                Status = f.Status,
                FlightClass = f.FlightClass.Name,
                Seat = f.Seat,
                DepartureTime = f.DepartureTime,
                ArrivalTime = f.ArrivalTime,
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
                        FlightClass = l.FlightClass != null ? l.FlightClass.Name : null,
                        Seat = l.Seat,
                    }).ToList(),
            } })
            .ToListAsync(ct);

        var data = rawRows.Select(x =>
        {
            x.Row.FlightType = FlightTypeCode(x.Type);
            return x.Row;
        }).ToList();

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
            // The booking's own times win when they're filled in; otherwise fall
            // back to the itinerary ends, as before.
            row.DepartureTime ??= first.StartTime;
            row.ArrivalTime ??= last.EndTime;
        }

        return ApiResponse<PaginatedResponse<EventFlightRow>>.SuccessResponse(
            new PaginatedResponse<EventFlightRow>(data, total, request.PageNumber, request.PageSize));
    }

    public async Task<ApiResponse<PaginatedResponse<EventAccommodationRow>>> GetEventAccommodationsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<PaginatedResponse<EventAccommodationRow>>.NotFoundResponse("Event not found");

        var query = _unitOfWork.Accommodations.Query().Where(a => a.Guest.EventId == ev.Id);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(a =>
                (a.Guest.FirstName + " " + a.Guest.LastName).Contains(term) ||
                a.Hotel.Name.Contains(term));
        }

        var total = await query.CountAsync(ct);

        var data = await query
            .OrderBy(a => a.Guest.FirstName).ThenBy(a => a.Guest.LastName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new EventAccommodationRow
            {
                Id = a.PublicId,
                GuestId = a.Guest.PublicId,
                GuestName = (a.Guest.FirstName + " " + a.Guest.LastName).Trim(),
                PhotoUrl = a.Guest.PhotoUrl,
                Organization = a.Guest.Organization,
                Tier = a.Guest.Tier,
                Hotel = a.Hotel.Name,
                HotelImageUrl = a.Hotel.ImageUrl,
                RoomType = a.RoomType.Name,
                CheckIn = a.CheckIn,
                CheckOut = a.CheckOut,
            })
            .ToListAsync(ct);

        return ApiResponse<PaginatedResponse<EventAccommodationRow>>.SuccessResponse(
            new PaginatedResponse<EventAccommodationRow>(data, total, request.PageNumber, request.PageSize));
    }

    public async Task<ApiResponse<PaginatedResponse<EventTransportRow>>> GetEventTransportsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<PaginatedResponse<EventTransportRow>>.NotFoundResponse("Event not found");

        var query = _unitOfWork.Transports.Query().Where(t => t.Guest.EventId == ev.Id);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(t =>
                (t.Guest.FirstName + " " + t.Guest.LastName).Contains(term) ||
                (t.Driver != null && (t.Driver.User.FirstName + " " + t.Driver.User.LastName).Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var data = await query
            .OrderBy(t => t.Guest.FirstName).ThenBy(t => t.Guest.LastName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new EventTransportRow
            {
                Id = t.PublicId,
                GuestId = t.Guest.PublicId,
                GuestName = (t.Guest.FirstName + " " + t.Guest.LastName).Trim(),
                PhotoUrl = t.Guest.PhotoUrl,
                Organization = t.Guest.Organization,
                Tier = t.Guest.Tier,
                Vehicle = t.Vehicle == null ? null : (t.Vehicle.VehicleNumber + " · " + t.Vehicle.VehicleModel),
                DriverId = t.Driver == null ? null : (Guid?)t.Driver.PublicId,
                DriverName = t.Driver == null ? null : (t.Driver.User.FirstName + " " + t.Driver.User.LastName).Trim(),
                DriverType = t.Driver == null ? null : (int?)t.Driver.DriverType,
                Pickup = t.PickupLocation.Address,
                Dropoff = t.DropoffLocation.Address,
                PickupTime = t.PickupTime,
                TripStatus = t.TripStatus,
            })
            .ToListAsync(ct);

        return ApiResponse<PaginatedResponse<EventTransportRow>>.SuccessResponse(
            new PaginatedResponse<EventTransportRow>(data, total, request.PageNumber, request.PageSize));
    }

    // Lowercase enum name — the code the API takes and returns for FlightType.
    private static string FlightTypeCode(FlightType t) => t.ToString().ToLowerInvariant();

    private static FlightType? ParseFlightType(string code)
        => Enum.TryParse<FlightType>((code ?? string.Empty).Trim(), ignoreCase: true, out var t)
            && Enum.IsDefined(t) ? t : null;

    public async Task<ApiResponse<PaginatedResponse<ArrivalDepartureRow>>> GetEventArrivalsDeparturesAsync(
        Guid eventId, ArrivalsDeparturesRequest request, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null) return ApiResponse<PaginatedResponse<ArrivalDepartureRow>>.NotFoundResponse("Event not found");

        var direction = (request.Direction ?? "all").Trim().ToLowerInvariant();
        var flights = _unitOfWork.Flights.Query();

        // Date window, applied to the flight's legs. Applied to `flights` itself
        // so it constrains both "which guests appear" and "which of their
        // flights are listed" — otherwise a matching guest would still show
        // their out-of-window bookings.
        if (request.FromDate.HasValue)
        {
            var from = request.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            flights = flights.Where(f => f.Legs.Any(l => l.StartTime >= from));
        }
        if (request.ToDate.HasValue)
        {
            // Exclusive upper bound on the next day, so the whole ToDate counts.
            var toExclusive = request.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            flights = flights.Where(f => f.Legs.Any(l => l.StartTime < toExclusive));
        }

        // One row per guest, so paging is over guests that actually have a
        // flight in the requested direction — not over flights.
        var guests = _unitOfWork.Guests.Query().Where(g => g.EventId == ev.Id);

        // A Return booking is both an arrival and a departure, so it belongs to
        // either direction.
        guests = direction switch
        {
            "inbound"  => guests.Where(g => flights.Any(f => f.GuestId == g.Id
                            && (f.FlightType == FlightType.Inbound || f.FlightType == FlightType.Return))),
            "outbound" => guests.Where(g => flights.Any(f => f.GuestId == g.Id
                            && (f.FlightType == FlightType.Outbound || f.FlightType == FlightType.Return))),
            _          => guests.Where(g => flights.Any(f => f.GuestId == g.Id)),
        };

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            guests = guests.Where(g =>
                (g.FirstName + " " + g.LastName).Contains(term) ||
                (g.Email != null && g.Email.Contains(term)) ||
                (g.Organization != null && g.Organization.Contains(term)) ||
                flights.Any(f => f.GuestId == g.Id && f.Legs.Any(l => l.FlightNumber.Contains(term))));
        }

        var total = await guests.CountAsync(ct);

        var page = await guests
            .OrderBy(g => g.FirstName).ThenBy(g => g.LastName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new ArrivalDepartureRow
            {
                GuestId = g.PublicId,
                GuestName = (g.FirstName + " " + g.LastName).Trim(),
                PhotoUrl = g.PhotoUrl,
                Email = g.Email,
                Organization = g.Organization,
                Tier = g.Tier,
            })
            .ToListAsync(ct);

        if (page.Count == 0)
        {
            return ApiResponse<PaginatedResponse<ArrivalDepartureRow>>.SuccessResponse(
                new PaginatedResponse<ArrivalDepartureRow>(page, total, request.PageNumber, request.PageSize));
        }

        // Second pass for the flights themselves: one query for the whole page,
        // then split by direction in memory.
        var guestIds = page.Select(r => r.GuestId).ToList();
        var rows = await flights
            .Where(f => guestIds.Contains(f.Guest.PublicId))
            .Select(f => new
            {
                GuestPublicId = f.Guest.PublicId,
                Type = f.FlightType,
                Flight = new ArrivalDepartureFlight
                {
                    Id = f.PublicId,
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
                            FlightClass = l.FlightClass != null ? l.FlightClass.Name : null,
                            Seat = l.Seat,
                        }).ToList(),
                    // Route + timings come from the itinerary ends: leave on the
                    // first leg, land on the last.
                    FlightNumber = f.Legs.OrderBy(l => l.StartTime).Select(l => l.FlightNumber).FirstOrDefault(),
                    DepartureCode = f.Legs.OrderBy(l => l.StartTime).Select(l => l.FromAirport.Code).FirstOrDefault(),
                    DepartureCity = f.Legs.OrderBy(l => l.StartTime).Select(l => l.FromAirport.City).FirstOrDefault(),
                    DepartureTime = f.Legs.OrderBy(l => l.StartTime).Select(l => l.StartTime).FirstOrDefault(),
                    ArrivalCode = f.Legs.OrderByDescending(l => l.StartTime).Select(l => l.ToAirport.Code).FirstOrDefault(),
                    ArrivalCity = f.Legs.OrderByDescending(l => l.StartTime).Select(l => l.ToAirport.City).FirstOrDefault(),
                    ArrivalTime = f.Legs.OrderByDescending(l => l.StartTime).Select(l => l.EndTime).FirstOrDefault(),
                },
            })
            .ToListAsync(ct);

        var byGuest = page.ToDictionary(r => r.GuestId);

        // Legacy rows exist whose FlightType is outside the enum (values 4 and 5
        // are present on the shared server). Those matched neither the Inbound
        // nor the Outbound exclusion below, so every one of them was added to
        // BOTH columns and the whole tab showed each flight twice. Undefined
        // types are separated out and placed by their timing instead: of a
        // guest's unclassified bookings the earliest is the arrival and the
        // latest is the departure, which is the only signal such a row carries.
        var classified = rows.Where(r => Enum.IsDefined(r.Type)).ToList();
        var unclassified = rows.Where(r => !Enum.IsDefined(r.Type)).ToList();

        foreach (var r in classified)
        {
            if (!byGuest.TryGetValue(r.GuestPublicId, out var row)) continue;
            r.Flight.FlightType = FlightTypeCode(r.Type);

            // A Return booking is listed in both columns — its legs cover the
            // arrival and the departure. When one direction is selected the other
            // stays empty, so the UI can drop that column entirely.
            if (r.Type != FlightType.Outbound && direction != "outbound") row.Inbound.Add(r.Flight);
            if (r.Type != FlightType.Inbound && direction != "inbound") row.Outbound.Add(r.Flight);
        }

        foreach (var group in unclassified.GroupBy(r => r.GuestPublicId))
        {
            if (!byGuest.TryGetValue(group.Key, out var row)) continue;

            var ordered = group.OrderBy(r => r.Flight.DepartureTime).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var flight = ordered[i].Flight;
                // A lone unclassified booking is treated as the arrival: it is
                // the more common single booking, and putting it in one column
                // is strictly better than repeating it in both.
                var isArrival = i == 0;
                flight.FlightType = FlightTypeCode(isArrival ? FlightType.Inbound : FlightType.Outbound);

                if (isArrival && direction != "outbound") row.Inbound.Add(flight);
                else if (!isArrival && direction != "inbound") row.Outbound.Add(flight);
            }

            _logger.LogWarning(
                "Guest {GuestId} has {Count} flight(s) with a FlightType outside the enum; " +
                "placed by departure time. Values: {Values}",
                group.Key, ordered.Count, string.Join(",", ordered.Select(r => (int)r.Type)));
        }

        foreach (var row in page)
        {
            row.Inbound = row.Inbound.OrderBy(f => f.DepartureTime).ToList();
            row.Outbound = row.Outbound.OrderBy(f => f.DepartureTime).ToList();
        }

        return ApiResponse<PaginatedResponse<ArrivalDepartureRow>>.SuccessResponse(
            new PaginatedResponse<ArrivalDepartureRow>(page, total, request.PageNumber, request.PageSize));
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
                var type = ParseFlightType(request.Flight.FlightType);
                if (type == null) return ApiResponse<bool>.ErrorResponse("Invalid flight type");

                // Return is one booking with two legs (outbound + inbound); the
                // one-way types are a single leg.
                var legInputs = request.Flight.Legs ?? [];
                var requiredLegs = type == FlightType.Return ? 2 : 1;
                if (legInputs.Count != requiredLegs)
                    return ApiResponse<bool>.ErrorResponse(
                        $"A {FlightTypeCode(type.Value)} flight needs exactly {requiredLegs} leg(s)");

                // Booking-level fallback — kept for backward compatibility with any
                // caller that still sends a flat FlightClassId/Seat instead of
                // per-leg ones (e.g. an older client). A real per-leg value below
                // always wins once one exists.
                var requestClassId = await ResolveNullableId(_unitOfWork.FlightClasses, request.Flight.FlightClassId, ct);

                Flight flight = null;
                if (request.Flight.Id is { } flightId && flightId != Guid.Empty)
                    flight = await _unitOfWork.Flights.Query().Include(f => f.Legs)
                        .FirstOrDefaultAsync(f => f.PublicId == flightId && f.GuestId == guest.Id, ct);

                var isNewFlight = flight == null;
                if (isNewFlight) flight = new Flight { GuestId = guest.Id };

                flight.FlightType = type.Value;
                flight.Status = request.Flight.Status;
                flight.DepartureTime = request.Flight.DepartureTime;
                flight.ArrivalTime = request.Flight.ArrivalTime;

                // Legs are matched on their public id so editing keeps the same
                // rows (the guest app references legs by id); anything the payload
                // left out is dropped. A return booking's two legs can carry
                // different fare classes/seats (e.g. Business outbound, Economy
                // inbound) — each leg owns its own.
                var keptLegs = new List<FlightLeg>();
                foreach (var li in legInputs)
                {
                    var leg = li.Id is { } legId && legId != Guid.Empty
                        ? flight.Legs.FirstOrDefault(l => l.PublicId == legId)
                        : null;
                    if (leg == null) { leg = new FlightLeg(); flight.Legs.Add(leg); }

                    leg.FlightNumber = li.FlightNumber;
                    leg.FromAirportId = await ResolveNullableId(_unitOfWork.AirportData, li.FromAirportId, ct);
                    leg.ToAirportId = await ResolveNullableId(_unitOfWork.AirportData, li.ToAirportId, ct);
                    leg.StartTime = li.StartTime;
                    leg.EndTime = li.EndTime;
                    leg.FlightClassId = await ResolveNullableId(_unitOfWork.FlightClasses, li.FlightClassId, ct);
                    leg.Seat = li.Seat;
                    keptLegs.Add(leg);
                }
                foreach (var stale in flight.Legs.Except(keptLegs).ToList())
                {
                    flight.Legs.Remove(stale);
                    _unitOfWork.FlightLegs.Remove(stale);
                }

                // Flight.FlightClassId/Seat are a "primary" copy mirrored from the
                // first leg (travel order) — same pattern as DepartureTime/
                // ArrivalTime — so anywhere that only shows one value (a collapsed
                // list row) still has something to display.
                var primaryLeg = keptLegs.FirstOrDefault();
                flight.FlightClassId = primaryLeg?.FlightClassId ?? requestClassId;
                flight.Seat = primaryLeg?.Seat ?? request.Flight.Seat;

                if (isNewFlight) await _unitOfWork.Flights.AddAsync(flight, ct);
            }

            if (request.Accommodation != null)
            {
                var hotelId = await ResolveId(_unitOfWork.AccommodationHotels, request.Accommodation.HotelId, ct);
                if (hotelId == null) return ApiResponse<bool>.ErrorResponse("Invalid hotel");
                var roomTypeId = await ResolveNullableId(_unitOfWork.AccommodationRoomTypes, request.Accommodation.RoomTypeId, ct);

                var checkIn = request.Accommodation.CheckIn;
                var checkOut = request.Accommodation.CheckOut;
                if (checkIn == null) return ApiResponse<bool>.ErrorResponse("Check-in date is required");
                if (checkOut == null) return ApiResponse<bool>.ErrorResponse("Check-out date is required");

                Accommodation acc = null;
                if (request.Accommodation.Id is { } accId && accId != Guid.Empty)
                    acc = await _unitOfWork.Accommodations.Query()
                        .FirstOrDefaultAsync(a => a.PublicId == accId && a.GuestId == guest.Id, ct);

                var isNewAcc = acc == null;

                // Room inventory: a hotel the event holds rooms at can't be
                // oversold. Hotels with no blocks are unmanaged and pass straight
                // through, which is what keeps events predating this module working.
                // An edit excludes itself, or its own nights would count twice.
                var full = await _inventory.CheckStayAvailabilityAsync(
                    guest.EventId, hotelId.Value, roomTypeId, checkIn.Value, checkOut.Value, acc?.Id, ct);
                if (full != null)
                    return ApiResponse<bool>.ConflictResponse(full, "ACCOMMODATION_UNAVAILABLE");

                if (isNewAcc) acc = new Accommodation { GuestId = guest.Id };

                acc.AccommodationHotelId = hotelId.Value;
                acc.RoomTypeId = roomTypeId;
                acc.CheckIn = checkIn;
                acc.CheckOut = checkOut;

                if (isNewAcc) await _unitOfWork.Accommodations.AddAsync(acc, ct);
            }

            // Self-service permission, not a booking — handled here rather than in
            // the Transport block below precisely so it can be granted with no
            // transport record at all (the guest then requests one from the app).
            if (request.AllowTransportRequest is { } allowTransport)
            {
                var services = GuestServices.Parse(guest.AllowedServicesJson);
                var transport = (int)GuestServiceType.Transport;
                if (allowTransport) services.Add(transport);
                else services.Remove(transport);
                guest.AllowedServicesJson = GuestServices.Serialize(services);
                _unitOfWork.Guests.Update(guest);
            }

            // Set when this save is what put a (new/different) driver on the trip —
            // notified after SaveChanges so the Transport has its row/PublicId.
            (Transport Transport, int DriverId)? newlyAssigned = null;

            if (request.Transport != null)
            {
                var pickupId = await ResolveNullableId(_unitOfWork.Locations, request.Transport.PickupLocationId, ct);
                var dropoffId = await ResolveNullableId(_unitOfWork.Locations, request.Transport.DropoffLocationId, ct);
                var vehicleId = await ResolveNullableId(_unitOfWork.Vehicles, request.Transport.VehicleId, ct);
                var driverId = await ResolveNullableId(_unitOfWork.DriverProfiles, request.Transport.DriverId, ct);
                if (request.Transport.DriverId is { } d && d != Guid.Empty && driverId == null)
                    return ApiResponse<bool>.ErrorResponse("Invalid driver");

                var pickupTime = request.Transport.PickupTime;
                var dropoffTime = request.Transport.DropoffTime;
                if (pickupTime == null)
                    return ApiResponse<bool>.ErrorResponse("Pickup time is required");
                if (dropoffTime == null)
                    return ApiResponse<bool>.ErrorResponse("Dropoff time is required");
                if (dropoffTime <= pickupTime)
                    return ApiResponse<bool>.ErrorResponse("Dropoff time must be after the pickup time");

                Transport tr = null;
                if (request.Transport.Id is { } trId && trId != Guid.Empty)
                    tr = await _unitOfWork.Transports.Query()
                        .FirstOrDefaultAsync(t => t.PublicId == trId && t.GuestId == guest.Id, ct);

                var isNewTransport = tr == null;
                var previousDriverId = tr?.DriverId;

                // Double-booking rules — this path had none, so the admin guest
                // form could quietly overbook a guest, a driver or a car that the
                // transportation screen would have refused. Editing a ride passes
                // its own id so it never clashes with itself.
                var conflict = await CheckTransportConflictsAsync(
                    guest.Id, driverId, vehicleId, pickupTime.Value, dropoffTime, tr?.Id, ct);
                if (conflict != null)
                    return ApiResponse<bool>.ConflictResponse(conflict, "TRANSPORTATION_CONFLICT");

                if (isNewTransport) tr = new Transport { GuestId = guest.Id };

                tr.PickupLocationId = pickupId;
                tr.DropoffLocationId = dropoffId;
                tr.VehicleId = vehicleId;
                tr.DriverId = driverId;
                // Status follows the driver assignment while the job hasn't started
                // yet; once the driver has moved it on (arrived / in-progress /
                // completed) the admin form must not drag it backwards. The actual
                // times are the driver app's to write, never blanked from here.
                if (isNewTransport || tr.TripStatus is null
                    or TransportStatuses.Pending or TransportStatuses.Assigned)
                    tr.TripStatus = driverId.HasValue ? TransportStatuses.Assigned : TransportStatuses.Pending;

                tr.ActualPickupTime = request.Transport.ActualPickupTime ?? tr.ActualPickupTime;
                tr.ActualDropOffTime = request.Transport.ActualDropOffTime ?? tr.ActualDropOffTime;

                tr.PickupTime = pickupTime;
                tr.DropoffTime = dropoffTime;

                if (isNewTransport) await _unitOfWork.Transports.AddAsync(tr, ct);

                // Re-saving the same driver is not an assignment — don't re-notify.
                if (driverId.HasValue && driverId != previousDriverId)
                    newlyAssigned = (tr, driverId.Value);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            if (newlyAssigned is { } assigned)
                await _notifications.SendToDriverAsync(_unitOfWork, assigned.DriverId,
                    NotificationTemplates.TransportDriverAssigned,
                    assigned.Transport.Tokens(guest), ct);

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

    // First conflict message across guest / driver / vehicle, or null if the slot
    // is free. Same validator the transportation screen uses, so both flows
    // enforce one rule.
    private async Task<string> CheckTransportConflictsAsync(
        int guestId, int? driverId, int? vehicleId,
        DateTime pickupTime, DateTime? dropoffTime, int? excludeTransportId, CancellationToken ct)
    {
        var guestConflict = await _conflictValidator.CheckGuestConflictAsync(guestId, pickupTime, excludeTransportId, ct);
        if (guestConflict.HasConflict) return guestConflict.Message;

        if (driverId.HasValue)
        {
            var driverConflict = await _conflictValidator.CheckDriverConflictAsync(driverId.Value, pickupTime, excludeTransportId, ct);
            if (driverConflict.HasConflict) return driverConflict.Message;
        }

        if (vehicleId.HasValue)
        {
            var vehicleConflict = await _conflictValidator.CheckVehicleConflictAsync(
                vehicleId.Value, pickupTime, dropoffTime, excludeTransportId, ct);
            if (vehicleConflict.HasConflict) return vehicleConflict.Message;
        }

        return null;
    }

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
            // Required, not optional: the VIP app shows the hotel's address on the
            // accommodation screen and the home check-in card. A blank one there
            // leaves the guest with a hotel name and no way to find it.
            if (string.IsNullOrWhiteSpace(request.Address))
                return ApiResponse<HotelDto>.ErrorResponse("Address is required");

            var hotel = new AccommodationHotel
            {
                Name = request.Name.Trim(),
                Address = request.Address.Trim(),
                ImageUrl = request.ImageUrl?.Trim(),
                LocationId = await ResolveNullableId(_unitOfWork.Locations, request.LocationId, ct)
            };
            hotel.SetCreationAudit(userId);
            await _unitOfWork.AccommodationHotels.AddAsync(hotel, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<HotelDto>.SuccessResponse(
                new HotelDto
                {
                    Id = hotel.PublicId, Name = hotel.Name, Address = hotel.Address,
                    ImageUrl = hotel.ImageUrl, LocationId = request.LocationId
                }, "Hotel created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hotel");
            return ApiResponse<HotelDto>.ServerErrorResponse("An error occurred while creating the hotel");
        }
    }

    public async Task<ApiResponse<HotelDto>> UpdateHotelAsync(
        Guid id, CreateHotelRequest request, int userId, CancellationToken ct = default)
    {
        try
        {
            var hotel = await _unitOfWork.AccommodationHotels.GetByPublicIdAsync(id, ct);
            if (hotel == null) return ApiResponse<HotelDto>.NotFoundResponse("Hotel not found");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<HotelDto>.ErrorResponse("Name is required");
            if (string.IsNullOrWhiteSpace(request.Address))
                return ApiResponse<HotelDto>.ErrorResponse("Address is required");

            hotel.Name = request.Name.Trim();
            hotel.Address = request.Address.Trim();
            // Blank means "no image" — the edit form can clear one, so an empty
            // string has to null the column rather than be ignored.
            hotel.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
            // Only reassigned when the request carries one. The lookup form doesn't
            // expose the map location, so treating "absent" as "clear it" would
            // wipe a link the caller never saw.
            if (request.LocationId is { } locationId && locationId != Guid.Empty)
                hotel.LocationId = await ResolveNullableId(_unitOfWork.Locations, locationId, ct);

            hotel.SetUpdateAudit(userId);
            _unitOfWork.AccommodationHotels.Update(hotel);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<HotelDto>.SuccessResponse(
                new HotelDto
                {
                    Id = hotel.PublicId, Name = hotel.Name, Address = hotel.Address,
                    ImageUrl = hotel.ImageUrl
                }, "Hotel updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hotel {HotelId}", id);
            return ApiResponse<HotelDto>.ServerErrorResponse("An error occurred while updating the hotel");
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
