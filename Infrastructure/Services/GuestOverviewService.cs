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

            var query = _unitOfWork.Guests.QueryNoTracking();

            if (request.EventId is { } eventPublicId && eventPublicId != Guid.Empty)
            {
                var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventPublicId, ct);
                query = ev == null ? query.Where(_ => false) : query.Where(g => g.EventId == ev.Id);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(g =>
                    g.FirstName.ToLower().Contains(term) ||
                    g.LastName.ToLower().Contains(term) ||
                    (g.Email != null && g.Email.ToLower().Contains(term)) ||
                    (g.Organization != null && g.Organization.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(request.Tier))
            {
                var tier = request.Tier.ToLower();
                query = query.Where(g => g.Tier != null && g.Tier.ToLower() == tier);
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
                query = query.Where(g => nat != null && g.NationalityId == nat.Id);
            }

            if (request.SessionId is { } sessionPublicId && sessionPublicId != Guid.Empty)
            {
                var session = await _unitOfWork.Sessions.GetByPublicIdAsync(sessionPublicId, ct);
                query = session == null
                    ? query.Where(_ => false)
                    : query.Where(g => guestSessions.Any(gs => gs.GuestId == g.Id && gs.SessionId == session.Id));
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
                        (includesNotSent && !invitations.Any(i => i.GuestId == g.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                        || (otherStatuses.Count > 0 && invitations.Any(i => i.GuestId == g.Id && otherStatuses.Contains(i.InvitationStatus))));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.AccreditationStatus))
            {
                if (request.AccreditationStatus == "not_required")
                    query = query.Where(g => !g.AccreditationRequired);
                else if (request.AccreditationStatus == GuestAccreditationStatus.Issued)
                    query = query.Where(g => g.AccreditationRequired && invitations.Any(i => i.GuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
                else if (request.AccreditationStatus == "pending")
                    query = query.Where(g => g.AccreditationRequired && !invitations.Any(i => i.GuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
            }

            if (request.ArrivalFrom.HasValue)
                query = query.Where(g => g.ArrivalDate != null && g.ArrivalDate >= request.ArrivalFrom.Value);
            if (request.ArrivalTo.HasValue)
                query = query.Where(g => g.ArrivalDate != null && g.ArrivalDate <= request.ArrivalTo.Value);
            if (request.DepartureFrom.HasValue)
                query = query.Where(g => g.DepartureDate != null && g.DepartureDate >= request.DepartureFrom.Value);
            if (request.DepartureTo.HasValue)
                query = query.Where(g => g.DepartureDate != null && g.DepartureDate <= request.DepartureTo.Value);

            // Counts/flags are correlated subqueries done inside the same projection
            // — one round trip for every row this filter set matches, not per guest.
            var raw = await query
                .Select(g => new
                {
                    g.PublicId,
                    g.FirstName,
                    g.LastName,
                    g.Email,
                    g.PhotoUrl,
                    g.GuestType,
                    EventId = g.Event.PublicId,
                    EventTitle = g.Event.Title,
                    g.Organization,
                    NationalityName = g.Nationality != null ? g.Nationality.Name : null,
                    NationalityFlag = g.Nationality != null ? g.Nationality.Flag : null,
                    g.Tier,
                    ServiceLevelId = g.ServiceLevel != null ? g.ServiceLevel.PublicId : (Guid?)null,
                    ServiceLevelName = g.ServiceLevel != null ? g.ServiceLevel.Name : null,
                    ServiceLevelColor = g.ServiceLevel != null ? g.ServiceLevel.Color : null,
                    InvitationStatus = invitations.Where(i => i.GuestId == g.Id).Select(i => i.InvitationStatus).FirstOrDefault() ?? GuestInvitationStatus.NotSent,
                    AccreditationStatus = invitations.Where(i => i.GuestId == g.Id).Select(i => i.AccreditationStatus).FirstOrDefault() ?? GuestAccreditationStatus.NotIssued,
                    g.ArrivalDate,
                    g.DepartureDate,
                    SessionsCount = guestSessions.Count(gs => gs.GuestId == g.Id),
                    ServicesCount = serviceEntries.Count(e => e.GuestId == g.Id),
                    PendingServicesCount = serviceEntries.Count(e => e.GuestId == g.Id && e.Status == "pending"),
                    SeatsCount = seatAssigns.Count(sa => sa.GuestId == g.Id),
                    HasFlight = flights.Any(f => f.GuestId == g.Id),
                    HasAccommodation = accommodations.Any(a => a.GuestId == g.Id),
                    HasTransport = transports.Any(t => t.GuestId == g.Id),
                    g.CreatedAt,
                })
                .ToListAsync(ct);

            // The same person gets a brand-new Guest row per event (no shared
            // person identity in the data model — see Guest.cs "identity is now
            // (Event, Person, ServiceLevel)"). Email is the one field every guest
            // is required to have, so it's the grouping key: one row per person,
            // not one per booking. Aggregating a variable number of sibling rows
            // (sum counts, OR flags, min/max dates) isn't a clean single GROUP BY
            // projection in EF, and at guest-table scale (hundreds/thousands, not
            // millions) one filtered round trip plus an in-memory GroupBy is
            // simpler and just as fast in practice.
            var grouped = raw
                .GroupBy(g => string.IsNullOrWhiteSpace(g.Email) ? g.PublicId.ToString() : g.Email.Trim().ToLowerInvariant())
                .Select(grp =>
                {
                    var primary = grp.OrderByDescending(r => r.CreatedAt).First();
                    return new GuestOverviewRow
                    {
                        Id = primary.PublicId,
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
                        ArrivalDate = grp.Select(r => r.ArrivalDate).Where(d => d != null).OrderBy(d => d).FirstOrDefault(),
                        DepartureDate = grp.Select(r => r.DepartureDate).Where(d => d != null).OrderByDescending(d => d).FirstOrDefault(),
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
            var anchor = await _unitOfWork.Guests.QueryNoTracking()
                .FirstOrDefaultAsync(g => g.PublicId == guestId, ct);
            if (anchor == null)
                return ApiResponse<GuestOverviewDetailResponse>.NotFoundResponse("Guest not found");

            // Every Guest row this person holds — one per event, see
            // GetGuestOverviewAsync for why email is the grouping key. Sections
            // below flatten across all of them, each item tagged with its event.
            var siblings = string.IsNullOrWhiteSpace(anchor.Email)
                ? new List<Guest> { anchor }
                : await _unitOfWork.Guests.QueryNoTracking()
                    .Include(g => g.Event)
                    .Include(g => g.ServiceLevel)
                    .Where(g => g.Email != null && g.Email.ToLower() == anchor.Email.ToLower())
                    .OrderBy(g => g.Event.StartDate)
                    .ToListAsync(ct);
            if (siblings.Count == 0)
                siblings.Add(anchor);

            var detail = new GuestOverviewDetailResponse { Id = anchor.PublicId, Email = anchor.Email };

            foreach (var g in siblings)
            {
                var invitation = await _unitOfWork.Invitations.QueryNoTracking()
                    .FirstOrDefaultAsync(i => i.GuestId == g.Id, ct);

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
                    ArrivalDate = g.ArrivalDate,
                    DepartureDate = g.DepartureDate,
                });

                var eventTitle = g.Event?.Title;

                detail.Sessions.AddRange(await _unitOfWork.GuestSessions.QueryNoTracking()
                    .Where(gs => gs.GuestId == g.Id)
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
                    .Where(f => f.GuestId == g.Id)
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
                    .Where(a => a.GuestId == g.Id)
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
                    .Where(t => t.GuestId == g.Id)
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
