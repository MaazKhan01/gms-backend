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

// System-wide guest listing + on-demand per-guest detail for the "Guest
// Overview" screen. Kept separate from GuestService: that one is scoped to a
// single event everywhere (GetGuestsAsync(eventId, ...)), and threading an
// "optional event" concern through its filters/queries would have touched
// every existing call site. This service only reads — sections it doesn't own
// (seating, dynamic services) are pulled from the services that do.
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

            var query = _unitOfWork.EventGuests.QueryNoTracking();

            if (request.EventId is { } eventPublicId && eventPublicId != Guid.Empty)
            {
                var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventPublicId, ct);
                query = ev == null ? query.Where(_ => false) : query.Where(g => g.EventId == ev.Id);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(g =>
                    g.Guest.FirstName.ToLower().Contains(term) ||
                    g.Guest.LastName.ToLower().Contains(term) ||
                    (g.Guest.Email != null && g.Guest.Email.ToLower().Contains(term)) ||
                    (g.Organization != null && g.Organization.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(request.Tier))
            {
                var tier = request.Tier.ToLower();
                query = query.Where(g => g.ServiceLevel != null && g.ServiceLevel.Name.ToLower() == tier);
            }

            if (!string.IsNullOrWhiteSpace(request.GuestType))
            {
                var type = request.GuestType.ToLower();
                query = query.Where(g => g.GuestType != null && g.GuestType.ToLower() == type);
            }

            if (request.ServiceLevelId is { } levelPublicId && levelPublicId != Guid.Empty)
            {
                var level = await _unitOfWork.ServiceLevels.QueryNoTracking()
                    .FirstOrDefaultAsync(l => l.PublicId == levelPublicId, ct);
                query = level == null ? query.Where(_ => false) : query.Where(g => g.ServiceLevelId == level.Id);
            }

            if (request.OrganizationId is { } orgPublicId && orgPublicId != Guid.Empty)
            {
                var org = await _unitOfWork.Organizations.GetByPublicIdAsync(orgPublicId, ct);
                query = query.Where(g => org != null && g.OrganizationId == org.Id);
            }

            if (request.NationalityId is { } natPublicId && natPublicId != Guid.Empty)
            {
                var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(natPublicId, ct);
                query = query.Where(g => nat != null && g.Guest.NationalityId == nat.Id);
            }

            if (request.SessionId is { } sessionPublicId && sessionPublicId != Guid.Empty)
            {
                var session = await _unitOfWork.Sessions.GetByPublicIdAsync(sessionPublicId, ct);
                query = session == null
                    ? query.Where(_ => false)
                    : query.Where(g => guestSessions.Any(gs => gs.EventGuestId == g.Id && gs.SessionId == session.Id));
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
                    var otherStatuses = statuses.Where(s => s != GuestInvitationStatus.NotSent).ToList();
                    query = query.Where(g =>
                        (includesNotSent && !invitations.Any(i => i.EventGuestId == g.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                        || (otherStatuses.Count > 0 && invitations.Any(i => i.EventGuestId == g.Id && otherStatuses.Contains(i.InvitationStatus))));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.AccreditationStatus))
            {
                if (request.AccreditationStatus == "not_required")
                    query = query.Where(g => !g.AccreditationRequired);
                else if (request.AccreditationStatus == GuestAccreditationStatus.Issued)
                    query = query.Where(g => g.AccreditationRequired && invitations.Any(i => i.EventGuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
                else if (request.AccreditationStatus == "pending")
                    query = query.Where(g => g.AccreditationRequired && !invitations.Any(i => i.EventGuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
            }

            // Bounded against the flight booking's own times rather than a column on
            // the guest: arrival/departure are the itinerary's, and duplicating them
            // on the guest let the two disagree.
            if (request.ArrivalFrom is { } af)
            {
                var t = af.ToDateTime(TimeOnly.MinValue);
                query = query.Where(g => flights.Any(f => f.EventGuestId == g.Id && f.ArrivalTime >= t));
            }
            if (request.ArrivalTo is { } at)
            {
                var t = at.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(g => flights.Any(f => f.EventGuestId == g.Id && f.ArrivalTime <= t));
            }
            if (request.DepartureFrom is { } df)
            {
                var t = df.ToDateTime(TimeOnly.MinValue);
                query = query.Where(g => flights.Any(f => f.EventGuestId == g.Id && f.DepartureTime >= t));
            }
            if (request.DepartureTo is { } dt)
            {
                var t = dt.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(g => flights.Any(f => f.EventGuestId == g.Id && f.DepartureTime <= t));
            }

            // Counts/flags are correlated subqueries done inside the same projection
            // — one round trip for every row this filter set matches, not per guest.
            var raw = await query
                .Select(g => new
                {
                    g.PublicId,
                    // The person behind this participation — the grouping key below.
                    PersonId = g.Guest.PublicId,
                    g.GuestId,
                    g.Guest.FirstName,
                    g.Guest.LastName,
                    g.Guest.Email,
                    g.Guest.PhotoUrl,
                    g.GuestType,
                    EventId = g.Event.PublicId,
                    EventTitle = g.Event.Title,
                    g.Organization,
                    NationalityName = g.Guest.Nationality != null ? g.Guest.Nationality.Name : null,
                    NationalityFlag = g.Guest.Nationality != null ? g.Guest.Nationality.Flag : null,
                    Tier = g.ServiceLevel != null ? g.ServiceLevel.Name : null,
                    ServiceLevelId = g.ServiceLevel != null ? g.ServiceLevel.PublicId : (Guid?)null,
                    ServiceLevelName = g.ServiceLevel != null ? g.ServiceLevel.Name : null,
                    ServiceLevelColor = g.ServiceLevel != null ? g.ServiceLevel.Color : null,
                    InvitationStatus = invitations.Where(i => i.EventGuestId == g.Id).Select(i => i.InvitationStatus).FirstOrDefault() ?? GuestInvitationStatus.NotSent,
                    AccreditationStatus = invitations.Where(i => i.EventGuestId == g.Id).Select(i => i.AccreditationStatus).FirstOrDefault() ?? GuestAccreditationStatus.NotIssued,
                    // Earliest landing / latest take-off across this participation's
                    // flights, since that is where these dates actually live now.
                    ArrivalAt = flights.Where(f => f.EventGuestId == g.Id && f.ArrivalTime != null)
                        .Min(f => f.ArrivalTime),
                    DepartureAt = flights.Where(f => f.EventGuestId == g.Id && f.DepartureTime != null)
                        .Max(f => f.DepartureTime),
                    SessionsCount = guestSessions.Count(gs => gs.EventGuestId == g.Id),
                    ServicesCount = serviceEntries.Count(e => e.EventGuestId == g.Id),
                    PendingServicesCount = serviceEntries.Count(e => e.EventGuestId == g.Id && e.Status == "pending"),
                    SeatsCount = seatAssigns.Count(sa => sa.EventGuestId == g.Id),
                    HasFlight = flights.Any(f => f.EventGuestId == g.Id),
                    HasAccommodation = accommodations.Any(a => a.EventGuestId == g.Id),
                    HasTransport = transports.Any(t => t.EventGuestId == g.Id),
                    g.CreatedAt,
                })
                .ToListAsync(ct);

            // One row per PERSON, several participations folded into it. Grouped on
            // the real Guests.Id now — this used to group on Email because the model
            // had no shared person identity, which meant two different humans sharing
            // an inbox collapsed into one row. Aggregating a variable number of
            // sibling rows (sum counts, OR flags, min/max dates) still isn't a clean
            // single GROUP BY projection in EF, and at guest-table scale
            // (hundreds/thousands, not millions) one filtered round trip plus an
            // in-memory GroupBy is simpler and just as fast in practice.
            var grouped = raw
                .GroupBy(g => g.GuestId)
                .Select(grp =>
                {
                    var primary = grp.OrderByDescending(r => r.CreatedAt).First();
                    return new GuestOverviewRow
                    {
                        Id = primary.PublicId,
                        PersonId = primary.PersonId,
                        FirstName = primary.FirstName,
                        LastName = primary.LastName,
                        Email = primary.Email,
                        PhotoUrl = primary.PhotoUrl,
                        GuestType = primary.GuestType,
                        EventId = primary.EventId,
                        EventTitle = primary.EventTitle,
                        EventsCount = grp.Select(r => r.EventId).Distinct().Count(),
                        EventTitles = grp.Select(r => r.EventTitle).Where(t => t != null).Distinct().ToList(),
                        Organization = primary.Organization,
                        NationalityName = primary.NationalityName,
                        NationalityFlag = primary.NationalityFlag,
                        Tier = primary.Tier,
                        ServiceLevelId = primary.ServiceLevelId,
                        ServiceLevelName = primary.ServiceLevelName,
                        ServiceLevelColor = primary.ServiceLevelColor,
                        InvitationStatus = primary.InvitationStatus,
                        AccreditationStatus = primary.AccreditationStatus,
                        ArrivalDate = grp.Select(r => r.ArrivalAt).Where(d => d != null)
                            .OrderBy(d => d).Select(d => (DateOnly?)DateOnly.FromDateTime(d.Value)).FirstOrDefault(),
                        DepartureDate = grp.Select(r => r.DepartureAt).Where(d => d != null)
                            .OrderByDescending(d => d).Select(d => (DateOnly?)DateOnly.FromDateTime(d.Value)).FirstOrDefault(),
                        SessionsCount = grp.Sum(r => r.SessionsCount),
                        ServicesCount = grp.Sum(r => r.ServicesCount),
                        PendingServicesCount = grp.Sum(r => r.PendingServicesCount),
                        SeatsCount = grp.Sum(r => r.SeatsCount),
                        HasFlight = grp.Any(r => r.HasFlight),
                        HasAccommodation = grp.Any(r => r.HasAccommodation),
                        HasTransport = grp.Any(r => r.HasTransport),
                        CreatedAt = grp.Min(r => r.CreatedAt),
                    };
                })
                .AsEnumerable();

            // These four flags are now an OR across a person's events, so they can
            // only be tested after grouping.
            if (request.HasFlight.HasValue)
                grouped = grouped.Where(r => r.HasFlight == request.HasFlight.Value);
            if (request.HasAccommodation.HasValue)
                grouped = grouped.Where(r => r.HasAccommodation == request.HasAccommodation.Value);
            if (request.HasTransport.HasValue)
                grouped = grouped.Where(r => r.HasTransport == request.HasTransport.Value);
            if (request.HasPendingServices.HasValue)
                grouped = grouped.Where(r => (r.PendingServicesCount > 0) == request.HasPendingServices.Value);

            var people = grouped.ToList();
            var total = people.Count;

            IEnumerable<GuestOverviewRow> ordered = request.SortBy?.ToLowerInvariant() switch
            {
                "organization" => request.SortDescending ? people.OrderByDescending(r => r.Organization) : people.OrderBy(r => r.Organization),
                "arrival" => request.SortDescending ? people.OrderByDescending(r => r.ArrivalDate) : people.OrderBy(r => r.ArrivalDate),
                "created" => request.SortDescending ? people.OrderByDescending(r => r.CreatedAt) : people.OrderBy(r => r.CreatedAt),
                _ => request.SortDescending ? people.OrderByDescending(r => r.FirstName) : people.OrderBy(r => r.FirstName),
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

    public async Task<ApiResponse<GuestOverviewDetailResponse>> GetGuestOverviewDetailAsync(Guid guestId, CancellationToken ct = default)
    {
        try
        {
            // `guestId` may be either the person (GuestOverviewRow.PersonId) or one
            // of their participations (GuestOverviewRow.Id) — both identify the same
            // human, so either is accepted rather than making callers care.
            var person = await _unitOfWork.Guests.QueryNoTracking()
                .FirstOrDefaultAsync(g => g.PublicId == guestId, ct)
                ?? await _unitOfWork.EventGuests.QueryNoTracking()
                    .Where(eg => eg.PublicId == guestId)
                    .Select(eg => eg.Guest)
                    .FirstOrDefaultAsync(ct);
            if (person == null)
                return ApiResponse<GuestOverviewDetailResponse>.NotFoundResponse("Guest not found");

            // Every event this person is in. A plain join now — this used to
            // re-match sibling rows on email because there was no person identity
            // to join on. Sections below flatten across all of them, each item
            // tagged with its event.
            var siblings = await _unitOfWork.EventGuests.QueryNoTracking()
                .Include(g => g.Event)
                .Include(g => g.ServiceLevel)
                .Where(g => g.GuestId == person.Id)
                .OrderBy(g => g.Event.StartDate)
                .ToListAsync(ct);

            var detail = new GuestOverviewDetailResponse { Id = person.PublicId, Email = person.Email };

            foreach (var g in siblings)
            {
                var invitation = await _unitOfWork.Invitations.QueryNoTracking()
                    .FirstOrDefaultAsync(i => i.EventGuestId == g.Id, ct);

                // Off the flight booking, not off the guest — see the list query.
                var itinerary = await _unitOfWork.Flights.QueryNoTracking()
                    .Where(x => x.EventGuestId == g.Id)
                    .GroupBy(x => 1)
                    .Select(grp => new
                    {
                        Arrival = grp.Min(x => x.ArrivalTime),
                        Departure = grp.Max(x => x.DepartureTime),
                    })
                    .FirstOrDefaultAsync(ct);

                detail.Events.Add(new GuestOverviewEventBlock
                {
                    GuestId = g.PublicId,
                    EventId = g.Event?.PublicId ?? Guid.Empty,
                    EventTitle = g.Event?.Title,
                    EventType = g.Event?.Type,
                    StartDate = g.Event?.StartDate,
                    EndDate = g.Event?.EndDate,
                    VenueName = g.Event?.VenueName,
                    ServiceLevelName = g.ServiceLevel?.Name,
                    ServiceLevelColor = g.ServiceLevel?.Color,
                    InvitationStatus = invitation?.InvitationStatus ?? GuestInvitationStatus.NotSent,
                    AccreditationStatus = invitation?.AccreditationStatus ?? GuestAccreditationStatus.NotIssued,
                    ArrivalDate = itinerary?.Arrival is { } a ? DateOnly.FromDateTime(a) : null,
                    DepartureDate = itinerary?.Departure is { } d ? DateOnly.FromDateTime(d) : null,
                });

                var eventTitle = g.Event?.Title;

                detail.Sessions.AddRange(await _unitOfWork.GuestSessions.QueryNoTracking()
                    .Where(gs => gs.EventGuestId == g.Id)
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
                    .Where(f => f.EventGuestId == g.Id)
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
                    .Where(a => a.EventGuestId == g.Id)
                    .Select(a => new GuestOverviewAccommodationRow
                    {
                        EventTitle = eventTitle,
                        Id = a.PublicId,
                        Hotel = a.Hotel != null ? a.Hotel.Name : null,
                        HotelImageUrl = a.Hotel != null ? a.Hotel.ImageUrl : null,
                        RoomType = a.RoomType != null ? a.RoomType.Name : null,
                        CheckIn = a.CheckIn,
                        CheckOut = a.CheckOut,
                        ImageUrl = a.ImageUrl,
                    })
                    .ToListAsync(ct));

                detail.Transport.AddRange(await _unitOfWork.Transports.QueryNoTracking()
                    .Where(t => t.EventGuestId == g.Id)
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

                var seatResult = await _seatingService.GetGuestSeatAssignmentsAsync(g.PublicId, ct);
                detail.Seatings.AddRange((seatResult.Data ?? new List<GuestSeatAssignmentDto>())
                    .Select(s => new GuestOverviewSeatRow
                    {
                        EventTitle = s.EventTitle,
                        SessionTitle = s.SessionTitle,
                        SeatCode = s.SeatCode,
                    }));

                // Flight/Accommodation/Transport are system slots on this same
                // plan — already covered by the three sections above.
                var planResult = await _serviceCatalogService.GetGuestServicePlanAsync(g.PublicId, ct);
                detail.OtherServices.AddRange((planResult.Data?.Slots ?? new())
                    .Where(s => !s.IsSystem)
                    .Select(s => new GuestOverviewOtherServiceRow
                    {
                        EventTitle = eventTitle,
                        ServiceId = s.ServiceId,
                        Name = s.Name,
                        NameAr = s.NameAr,
                        Icon = s.Icon,
                        Status = s.Status,
                        IsUnlocked = s.IsUnlocked,
                        LockedReason = s.LockedReason,
                        Entries = s.Entries,
                    }));
            }

            return ApiResponse<GuestOverviewDetailResponse>.SuccessResponse(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest overview detail for guest {GuestId}", guestId);
            return ApiResponse<GuestOverviewDetailResponse>.ServerErrorResponse("An error occurred while retrieving guest detail");
        }
    }
}
