using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Guest;
using Core.ViewModel.Seating;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// System-wide guest listing + on-demand per-person detail for the "Guest
// Overview" screen. Kept separate from GuestService: that one is scoped to a
// single event everywhere (GetGuestsAsync(eventId, ...)), and threading an
// "optional event" concern through its filters/queries would have touched
// every existing call site. This service only reads — sections it doesn't own
// (seating, dynamic services) are pulled from the services that do.
//
// One row per PERSON here, one section per participation inside the detail:
// Guest is the cross-event identity, EventGuest is the per-event booking, so
// the grouping this screen needs is now a plain join rather than the
// group-by-email reconstruction it used to be.
public class GuestOverviewService(
    IUnitOfWork _unitOfWork,
    IServiceCatalogService _serviceCatalogService,
    ISeatingService _seatingService,
    ILogger<GuestOverviewService> _logger) : IGuestOverviewService
{
    public async Task<ApiResponse<PaginatedResponse<GuestOverviewRow>>> GetGuestOverviewAsync(
        GuestOverviewPagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var flights = _unitOfWork.Flights.QueryNoTracking();
            var accommodations = _unitOfWork.Accommodations.QueryNoTracking();
            var transports = _unitOfWork.Transports.QueryNoTracking();
            var seatAssigns = _unitOfWork.SeatAssigns.QueryNoTracking();
            var serviceEntries = _unitOfWork.GuestServiceEntries.QueryNoTracking();
            var invitations = _unitOfWork.Invitations.QueryNoTracking();
            var guestSessions = _unitOfWork.GuestSessions.QueryNoTracking();

            var query = _unitOfWork.Guests.QueryNoTracking();

            // Every filter below except the person's own name/email is a property
            // of a participation, so each is "has at least one EventGuest where…".
            // A person matches if any of their events does.
            if (request.EventId is { } eventPublicId && eventPublicId != Guid.Empty)
            {
                var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventPublicId, ct);
                query = ev == null
                    ? query.Where(_ => false)
                    : query.Where(g => g.EventGuests.Any(eg => eg.EventId == ev.Id));
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(g =>
                    g.FirstName.ToLower().Contains(term) ||
                    g.LastName.ToLower().Contains(term) ||
                    (g.Email != null && g.Email.ToLower().Contains(term)) ||
                    g.EventGuests.Any(eg => eg.Organization != null && eg.Organization.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(request.Tier))
            {
                var tier = request.Tier.ToLower();
                query = query.Where(g => g.EventGuests.Any(
                    eg => eg.ServiceLevel != null && eg.ServiceLevel.Code.ToLower() == tier));
            }

            if (!string.IsNullOrWhiteSpace(request.GuestType))
            {
                var type = request.GuestType.ToLower();
                query = query.Where(g => g.EventGuests.Any(eg => eg.GuestType != null && eg.GuestType.ToLower() == type));
            }

            if (request.ServiceLevelId is { } levelPublicId && levelPublicId != Guid.Empty)
            {
                var level = await _unitOfWork.ServiceLevels.QueryNoTracking()
                    .FirstOrDefaultAsync(l => l.PublicId == levelPublicId, ct);
                query = level == null
                    ? query.Where(_ => false)
                    : query.Where(g => g.EventGuests.Any(eg => eg.ServiceLevelId == level.Id));
            }

            if (request.OrganizationId is { } orgPublicId && orgPublicId != Guid.Empty)
            {
                var org = await _unitOfWork.Organizations.GetByPublicIdAsync(orgPublicId, ct);
                query = query.Where(g => org != null && g.EventGuests.Any(eg => eg.OrganizationId == org.Id));
            }

            // Nationality is person-level now, so this one is a plain column test.
            if (request.NationalityId is { } natPublicId && natPublicId != Guid.Empty)
            {
                var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(natPublicId, ct);
                query = query.Where(g => nat != null && g.NationalityId == nat.Id);
            }

            if (request.SessionId is { } sessionPublicId && sessionPublicId != Guid.Empty)
            {
                var session = await _unitOfWork.Sessions.GetByPublicIdAsync(sessionPublicId, ct);
                query = session == null
                    ? query.Where(_ => false)
                    : query.Where(g => g.EventGuests.Any(eg =>
                        guestSessions.Any(gs => gs.EventGuestId == eg.Id && gs.SessionId == session.Id)));
            }

            // Same "not_sent also covers no row at all" rule as GuestService.GetGuestsAsync.
            if (!string.IsNullOrWhiteSpace(request.InvitationStatus))
            {
                var statuses = request.InvitationStatus
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (statuses.Count > 0)
                {
                    var includesNotSent = statuses.Contains(GuestInvitationStatus.NotSent);
                    var otherStatuses = statuses.Where(x => x != GuestInvitationStatus.NotSent).ToList();
                    query = query.Where(g => g.EventGuests.Any(eg =>
                        (includesNotSent && !invitations.Any(i => i.EventGuestId == eg.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                        || (otherStatuses.Count > 0 && invitations.Any(i => i.EventGuestId == eg.Id && otherStatuses.Contains(i.InvitationStatus)))));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.AccreditationStatus))
            {
                if (request.AccreditationStatus == "not_required")
                    query = query.Where(g => g.EventGuests.Any(eg => !eg.AccreditationRequired));
                else if (request.AccreditationStatus == GuestAccreditationStatus.Issued)
                    query = query.Where(g => g.EventGuests.Any(eg => eg.AccreditationRequired
                        && invitations.Any(i => i.EventGuestId == eg.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued)));
                else if (request.AccreditationStatus == "pending")
                    query = query.Where(g => g.EventGuests.Any(eg => eg.AccreditationRequired
                        && !invitations.Any(i => i.EventGuestId == eg.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued)));
            }

            // Travel dates come from the flight bookings now (`flights`, declared
            // above). The filters are still whole-day bounds, so each DateOnly is
            // widened to a DateTime half-open range — an inclusive "to" is the START
            // of the next day, otherwise a flight landing at 14:00 on the bound date
            // would be excluded.
            if (request.ArrivalFrom.HasValue)
            {
                var from = request.ArrivalFrom.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(g => g.EventGuests.Any(eg =>
                    flights.Any(f => f.EventGuestId == eg.Id && f.ArrivalTime != null && f.ArrivalTime >= from)));
            }
            if (request.ArrivalTo.HasValue)
            {
                var to = request.ArrivalTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                query = query.Where(g => g.EventGuests.Any(eg =>
                    flights.Any(f => f.EventGuestId == eg.Id && f.ArrivalTime != null && f.ArrivalTime < to)));
            }
            if (request.DepartureFrom.HasValue)
            {
                var from = request.DepartureFrom.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(g => g.EventGuests.Any(eg =>
                    flights.Any(f => f.EventGuestId == eg.Id && f.DepartureTime != null && f.DepartureTime >= from)));
            }
            if (request.DepartureTo.HasValue)
            {
                var to = request.DepartureTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                query = query.Where(g => g.EventGuests.Any(eg =>
                    flights.Any(f => f.EventGuestId == eg.Id && f.DepartureTime != null && f.DepartureTime < to)));
            }

            // Counts/flags are correlated subqueries done inside the same projection
            // — one round trip for every row this filter set matches, not per guest.
            // Everything per-event is aggregated across the person's participations,
            // which is exactly what this cross-event screen is asking for.
            // One round trip for every person this filter set matches. Each
            // aggregate is a plain correlated subquery over the child table,
            // joined back through EventGuest — nesting them inside a Sum() over
            // the navigation collection reads nicer but is not something the
            // provider can translate.
            var rows = await query
                .Select(g => new GuestOverviewRow
                {
                    Id = g.PublicId,
                    FirstName = g.FirstName,
                    LastName = g.LastName,
                    Email = g.Email,
                    PhotoUrl = g.PhotoUrl,
                    NationalityName = g.Nationality != null ? g.Nationality.Name : null,
                    NationalityFlag = g.Nationality != null ? g.Nationality.Flag : null,

                    // "Primary" = their most recent participation, which is what the
                    // single-value columns (event, tier, organisation, statuses) show.
                    GuestType = g.EventGuests.OrderByDescending(eg => eg.CreatedAt).Select(eg => eg.GuestType).FirstOrDefault(),
                    EventId = g.EventGuests.OrderByDescending(eg => eg.CreatedAt).Select(eg => eg.Event.PublicId).FirstOrDefault(),
                    EventTitle = g.EventGuests.OrderByDescending(eg => eg.CreatedAt).Select(eg => eg.Event.Title).FirstOrDefault(),
                    EventsCount = g.EventGuests.Count,
                    // Deduped after materialisation — Distinct() inside a collection
                    // projection is not translatable.
                    EventTitles = g.EventGuests.Select(eg => eg.Event.Title).ToList(),
                    Organization = g.EventGuests.OrderByDescending(eg => eg.CreatedAt).Select(eg => eg.Organization).FirstOrDefault(),
                    Tier = g.EventGuests.OrderByDescending(eg => eg.CreatedAt)
                        .Select(eg => eg.ServiceLevel != null ? eg.ServiceLevel.Code : null).FirstOrDefault(),
                    ServiceLevelId = g.EventGuests.OrderByDescending(eg => eg.CreatedAt)
                        .Select(eg => eg.ServiceLevel != null ? eg.ServiceLevel.PublicId : (Guid?)null).FirstOrDefault(),
                    ServiceLevelName = g.EventGuests.OrderByDescending(eg => eg.CreatedAt)
                        .Select(eg => eg.ServiceLevel != null ? eg.ServiceLevel.Name : null).FirstOrDefault(),
                    ServiceLevelColor = g.EventGuests.OrderByDescending(eg => eg.CreatedAt)
                        .Select(eg => eg.ServiceLevel != null ? eg.ServiceLevel.Color : null).FirstOrDefault(),
                    InvitationStatus = invitations
                        .Where(i => i.EventGuest.GuestId == g.Id)
                        .OrderByDescending(i => i.EventGuest.CreatedAt)
                        .Select(i => i.InvitationStatus)
                        .FirstOrDefault() ?? GuestInvitationStatus.NotSent,
                    AccreditationStatus = invitations
                        .Where(i => i.EventGuest.GuestId == g.Id)
                        .OrderByDescending(i => i.EventGuest.CreatedAt)
                        .Select(i => i.AccreditationStatus)
                        .FirstOrDefault() ?? GuestAccreditationStatus.NotIssued,

                    // Earliest landing / latest take-off across every flight this
                    // person has on any participation. Narrowed to DateOnly below.
                    ArrivalTimeRaw = flights
                        .Where(f => f.EventGuest.GuestId == g.Id && f.ArrivalTime != null)
                        .Min(f => f.ArrivalTime),
                    DepartureTimeRaw = flights
                        .Where(f => f.EventGuest.GuestId == g.Id && f.DepartureTime != null)
                        .Max(f => f.DepartureTime),

                    SessionsCount = guestSessions.Count(gs => gs.EventGuest.GuestId == g.Id),
                    ServicesCount = serviceEntries.Count(e => e.EventGuest.GuestId == g.Id),
                    PendingServicesCount = serviceEntries.Count(e => e.EventGuest.GuestId == g.Id && e.Status == "pending"),
                    SeatsCount = seatAssigns.Count(sa => sa.EventGuest.GuestId == g.Id),
                    HasFlight = flights.Any(f => f.EventGuest.GuestId == g.Id),
                    HasAccommodation = accommodations.Any(a => a.EventGuest.GuestId == g.Id),
                    HasTransport = transports.Any(t => t.EventGuest.GuestId == g.Id),
                    CreatedAt = g.CreatedAt,
                })
                .ToListAsync(ct);

            foreach (var r in rows)
            {
                r.EventTitles = r.EventTitles.Where(t => t != null).Distinct().ToList();
                r.ArrivalDate = r.ArrivalTimeRaw.HasValue ? DateOnly.FromDateTime(r.ArrivalTimeRaw.Value) : null;
                r.DepartureDate = r.DepartureTimeRaw.HasValue ? DateOnly.FromDateTime(r.DepartureTimeRaw.Value) : null;
            }

            // These four are ORs across a person's events, which the projection
            // above has already collapsed — so they filter here rather than in SQL.
            IEnumerable<GuestOverviewRow> people = rows;
            if (request.HasFlight.HasValue)
                people = people.Where(r => r.HasFlight == request.HasFlight.Value);
            if (request.HasAccommodation.HasValue)
                people = people.Where(r => r.HasAccommodation == request.HasAccommodation.Value);
            if (request.HasTransport.HasValue)
                people = people.Where(r => r.HasTransport == request.HasTransport.Value);
            if (request.HasPendingServices.HasValue)
                people = people.Where(r => (r.PendingServicesCount > 0) == request.HasPendingServices.Value);

            var filtered = people.ToList();
            var total = filtered.Count;

            IEnumerable<GuestOverviewRow> ordered = request.SortBy?.ToLowerInvariant() switch
            {
                "organization" => request.SortDescending ? filtered.OrderByDescending(r => r.Organization) : filtered.OrderBy(r => r.Organization),
                "arrival" => request.SortDescending ? filtered.OrderByDescending(r => r.ArrivalDate) : filtered.OrderBy(r => r.ArrivalDate),
                "created" => request.SortDescending ? filtered.OrderByDescending(r => r.CreatedAt) : filtered.OrderBy(r => r.CreatedAt),
                _ => request.SortDescending ? filtered.OrderByDescending(r => r.FirstName) : filtered.OrderBy(r => r.FirstName),
            };

            var items = ordered
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var paged = new PaginatedResponse<GuestOverviewRow>(items, total, request.PageNumber, request.PageSize);
            return ApiResponse<PaginatedResponse<GuestOverviewRow>>.SuccessResponse(paged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest overview list");
            return ApiResponse<PaginatedResponse<GuestOverviewRow>>.ServerErrorResponse("An error occurred while retrieving guests");
        }
    }

    /// <summary><paramref name="personId"/> is a <c>Guest.PublicId</c> — this
    /// screen is the person's whole history, so it is keyed on the person, not on
    /// one of their participations.</summary>
    public async Task<ApiResponse<GuestOverviewDetailResponse>> GetGuestOverviewDetailAsync(Guid personId, CancellationToken ct = default)
    {
        try
        {
            var person = await _unitOfWork.Guests.QueryNoTracking()
                .FirstOrDefaultAsync(g => g.PublicId == personId, ct);
            if (person == null)
                return ApiResponse<GuestOverviewDetailResponse>.NotFoundResponse("Guest not found");

            // Every event this person participates in. Sections below flatten
            // across all of them, each item tagged with its event.
            var participations = await _unitOfWork.EventGuests.QueryNoTracking()
                .Include(eg => eg.Event)
                .Include(eg => eg.ServiceLevel)
                .Where(eg => eg.GuestId == person.Id)
                .OrderBy(eg => eg.Event.StartDate)
                .ToListAsync(ct);

            var detail = new GuestOverviewDetailResponse { Id = person.PublicId, Email = person.Email };

            foreach (var eg in participations)
            {
                var invitation = await _unitOfWork.Invitations.QueryNoTracking()
                    .FirstOrDefaultAsync(i => i.EventGuestId == eg.Id, ct);

                // Travel dates for this participation come off its flights now.
                // Earliest landing / latest take-off, so a return booking reads as
                // one trip. Null until something is booked.
                var egFlightTimes = await _unitOfWork.Flights.QueryNoTracking()
                    .Where(f => f.EventGuestId == eg.Id)
                    .Select(f => new { f.ArrivalTime, f.DepartureTime })
                    .ToListAsync(ct);
                var egArrival = egFlightTimes.Min(f => f.ArrivalTime);
                var egDeparture = egFlightTimes.Max(f => f.DepartureTime);

                detail.Events.Add(new GuestOverviewEventBlock
                {
                    EventGuestId = eg.PublicId,
                    EventId = eg.Event?.PublicId ?? Guid.Empty,
                    EventTitle = eg.Event?.Title,
                    EventType = eg.Event?.Type,
                    StartDate = eg.Event?.StartDate,
                    EndDate = eg.Event?.EndDate,
                    VenueName = eg.Event?.VenueName,
                    ServiceLevelName = eg.ServiceLevel?.Name,
                    ServiceLevelColor = eg.ServiceLevel?.Color,
                    InvitationStatus = invitation?.InvitationStatus ?? GuestInvitationStatus.NotSent,
                    AccreditationStatus = invitation?.AccreditationStatus ?? GuestAccreditationStatus.NotIssued,
                    ArrivalDate = egArrival.HasValue ? DateOnly.FromDateTime(egArrival.Value) : null,
                    DepartureDate = egDeparture.HasValue ? DateOnly.FromDateTime(egDeparture.Value) : null,
                });

                var eventTitle = eg.Event?.Title;

                detail.Sessions.AddRange(await _unitOfWork.GuestSessions.QueryNoTracking()
                    .Where(gs => gs.EventGuestId == eg.Id)
                    .Select(gs => new GuestOverviewSessionRow
                    {
                        EventTitle = eventTitle,
                        Id = gs.Session.PublicId,
                        Title = gs.Session.Title,
                        Date = gs.Session.Date,
                        Time = gs.Session.Time,
                        Room = gs.Session.Room,
                        Speaker = gs.Session.Speaker,
                        Status = gs.Status,
                    })
                    .ToListAsync(ct));

                detail.Flights.AddRange(await _unitOfWork.Flights.QueryNoTracking()
                    .Where(f => f.EventGuestId == eg.Id)
                    .Select(f => new GuestOverviewFlightRow
                    {
                        EventTitle = eventTitle,
                        Id = f.PublicId,
                        FlightType = f.FlightType.ToString(),
                        Status = f.Status,
                        FlightClass = f.FlightClass != null ? f.FlightClass.Name : null,
                        Seat = f.Seat,
                        DepartureTime = f.DepartureTime,
                        ArrivalTime = f.ArrivalTime,
                        ImageUrl = f.ImageUrl,
                        Legs = f.Legs.Select(l => new GuestOverviewFlightLegRow
                        {
                            FlightNumber = l.FlightNumber,
                            DepartureCode = l.FromAirport != null ? l.FromAirport.Code : null,
                            DepartureCity = l.FromAirport != null ? l.FromAirport.City : null,
                            ArrivalCode = l.ToAirport != null ? l.ToAirport.Code : null,
                            ArrivalCity = l.ToAirport != null ? l.ToAirport.City : null,
                            StartTime = l.StartTime,
                            EndTime = l.EndTime,
                            FlightClass = l.FlightClass != null ? l.FlightClass.Name : null,
                            Seat = l.Seat,
                        }).ToList(),
                    })
                    .ToListAsync(ct));

                detail.Accommodations.AddRange(await _unitOfWork.Accommodations.QueryNoTracking()
                    .Where(a => a.EventGuestId == eg.Id)
                    .Select(a => new GuestOverviewAccommodationRow
                    {
                        EventTitle = eventTitle,
                        Id = a.PublicId,
                        Hotel = a.Contract != null ? a.Contract.Hotel.Name : null,
                        HotelImageUrl = a.Contract != null ? a.Contract.Hotel.ImageUrl : null,
                        RoomType = a.RoomType != null ? a.RoomType.Name : null,
                        CheckIn = a.CheckIn,
                        CheckOut = a.CheckOut,
                        ImageUrl = a.ImageUrl,
                    })
                    .ToListAsync(ct));

                detail.Transport.AddRange(await _unitOfWork.Transports.QueryNoTracking()
                    .Where(t => t.EventGuestId == eg.Id)
                    .Select(t => new GuestOverviewTransportRow
                    {
                        EventTitle = eventTitle,
                        Id = t.PublicId,
                        TripStatus = t.TripStatus,
                        Vehicle = t.Vehicle != null ? $"{t.Vehicle.VehicleNumber} · {t.Vehicle.VehicleModel}" : null,
                        DriverName = t.Driver != null && t.Driver.User != null ? $"{t.Driver.User.FirstName} {t.Driver.User.LastName}".Trim() : null,
                        Pickup = t.PickupLocation != null ? t.PickupLocation.Address : null,
                        Dropoff = t.DropoffLocation != null ? t.DropoffLocation.Address : null,
                        PickupTime = t.PickupTime,
                        DropoffTime = t.DropoffTime,
                    })
                    .ToListAsync(ct));

                var seatResult = await _seatingService.GetGuestSeatAssignmentsAsync(eg.PublicId, ct);
                detail.Seatings.AddRange((seatResult.Data ?? new List<GuestSeatAssignmentDto>())
                    .Select(x => new GuestOverviewSeatRow
                    {
                        EventTitle = x.EventTitle,
                        SessionTitle = x.SessionTitle,
                        SeatCode = x.SeatCode,
                    }));

                // Flight/Accommodation/Transport are system slots on this same
                // plan — already covered by the three sections above.
                var planResult = await _serviceCatalogService.GetGuestServicePlanAsync(eg.PublicId, ct);
                detail.OtherServices.AddRange((planResult.Data?.Slots ?? new())
                    .Where(x => !x.IsSystem)
                    .Select(x => new GuestOverviewOtherServiceRow
                    {
                        EventTitle = eventTitle,
                        ServiceId = x.ServiceId,
                        Name = x.Name,
                        NameAr = x.NameAr,
                        Icon = x.Icon,
                        Status = x.Status,
                        IsUnlocked = x.IsUnlocked,
                        LockedReason = x.LockedReason,
                        Entries = x.Entries,
                    }));
            }

            return ApiResponse<GuestOverviewDetailResponse>.SuccessResponse(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest overview detail for person {PersonId}", personId);
            return ApiResponse<GuestOverviewDetailResponse>.ServerErrorResponse("An error occurred while retrieving guest detail");
        }
    }
}
