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

            if (request.HasFlight.HasValue)
                query = request.HasFlight.Value
                    ? query.Where(g => flights.Any(f => f.GuestId == g.Id))
                    : query.Where(g => !flights.Any(f => f.GuestId == g.Id));

            if (request.HasAccommodation.HasValue)
                query = request.HasAccommodation.Value
                    ? query.Where(g => accommodations.Any(a => a.GuestId == g.Id))
                    : query.Where(g => !accommodations.Any(a => a.GuestId == g.Id));

            if (request.HasTransport.HasValue)
                query = request.HasTransport.Value
                    ? query.Where(g => transports.Any(t => t.GuestId == g.Id))
                    : query.Where(g => !transports.Any(t => t.GuestId == g.Id));

            if (request.HasPendingServices.HasValue)
                query = request.HasPendingServices.Value
                    ? query.Where(g => serviceEntries.Any(e => e.GuestId == g.Id && e.Status == "pending"))
                    : query.Where(g => !serviceEntries.Any(e => e.GuestId == g.Id && e.Status == "pending"));

            if (request.ArrivalFrom.HasValue)
                query = query.Where(g => g.ArrivalDate != null && g.ArrivalDate >= request.ArrivalFrom.Value);
            if (request.ArrivalTo.HasValue)
                query = query.Where(g => g.ArrivalDate != null && g.ArrivalDate <= request.ArrivalTo.Value);
            if (request.DepartureFrom.HasValue)
                query = query.Where(g => g.DepartureDate != null && g.DepartureDate >= request.DepartureFrom.Value);
            if (request.DepartureTo.HasValue)
                query = query.Where(g => g.DepartureDate != null && g.DepartureDate <= request.DepartureTo.Value);

            var total = await query.CountAsync(ct);

            IQueryable<Guest> ordered = request.SortBy?.ToLowerInvariant() switch
            {
                "name" => request.SortDescending ? query.OrderByDescending(g => g.FirstName) : query.OrderBy(g => g.FirstName),
                "organization" => request.SortDescending ? query.OrderByDescending(g => g.Organization) : query.OrderBy(g => g.Organization),
                "arrival" => request.SortDescending ? query.OrderByDescending(g => g.ArrivalDate) : query.OrderBy(g => g.ArrivalDate),
                "created" => request.SortDescending ? query.OrderByDescending(g => g.CreatedAt) : query.OrderBy(g => g.CreatedAt),
                _ => request.SortDescending ? query.OrderByDescending(g => g.CreatedAt) : query.OrderBy(g => g.FirstName),
            };

            // Counts/flags are correlated subqueries done inside the same projection
            // (not a per-row round trip), and only run for the one page being returned.
            var items = await ordered
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(g => new GuestOverviewRow
                {
                    Id = g.PublicId,
                    FirstName = g.FirstName,
                    LastName = g.LastName,
                    Email = g.Email,
                    PhotoUrl = g.PhotoUrl,
                    GuestType = g.GuestType,
                    EventId = g.Event.PublicId,
                    EventTitle = g.Event.Title,
                    Organization = g.Organization,
                    NationalityName = g.Nationality != null ? g.Nationality.Name : null,
                    NationalityFlag = g.Nationality != null ? g.Nationality.Flag : null,
                    Tier = g.Tier,
                    ServiceLevelId = g.ServiceLevel != null ? g.ServiceLevel.PublicId : (Guid?)null,
                    ServiceLevelName = g.ServiceLevel != null ? g.ServiceLevel.Name : null,
                    ServiceLevelColor = g.ServiceLevel != null ? g.ServiceLevel.Color : null,
                    InvitationStatus = invitations.Where(i => i.GuestId == g.Id).Select(i => i.InvitationStatus).FirstOrDefault() ?? GuestInvitationStatus.NotSent,
                    AccreditationStatus = invitations.Where(i => i.GuestId == g.Id).Select(i => i.AccreditationStatus).FirstOrDefault() ?? GuestAccreditationStatus.NotIssued,
                    ArrivalDate = g.ArrivalDate,
                    DepartureDate = g.DepartureDate,
                    SessionsCount = guestSessions.Count(gs => gs.GuestId == g.Id),
                    ServicesCount = serviceEntries.Count(e => e.GuestId == g.Id),
                    PendingServicesCount = serviceEntries.Count(e => e.GuestId == g.Id && e.Status == "pending"),
                    SeatsCount = seatAssigns.Count(sa => sa.GuestId == g.Id),
                    HasFlight = flights.Any(f => f.GuestId == g.Id),
                    HasAccommodation = accommodations.Any(a => a.GuestId == g.Id),
                    HasTransport = transports.Any(t => t.GuestId == g.Id),
                    CreatedAt = g.CreatedAt,
                })
                .ToListAsync(ct);

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
            var guest = await _unitOfWork.Guests.QueryNoTracking()
                .Include(g => g.Event)
                .FirstOrDefaultAsync(g => g.PublicId == guestId, ct);
            if (guest == null)
                return ApiResponse<GuestOverviewDetailResponse>.NotFoundResponse("Guest not found");

            var detail = new GuestOverviewDetailResponse
            {
                Id = guest.PublicId,
                Event = guest.Event == null ? null : new GuestOverviewEventSection
                {
                    Id = guest.Event.PublicId,
                    Title = guest.Event.Title,
                    Type = guest.Event.Type,
                    StartDate = guest.Event.StartDate,
                    EndDate = guest.Event.EndDate,
                    VenueName = guest.Event.VenueName,
                },
            };

            detail.Sessions = await _unitOfWork.GuestSessions.QueryNoTracking()
                .Where(gs => gs.GuestId == guest.Id)
                .Select(gs => new GuestOverviewSessionRow
                {
                    Id = gs.Session.PublicId,
                    Title = gs.Session.Title,
                    Date = gs.Session.Date,
                    Time = gs.Session.Time,
                    Room = gs.Session.Room,
                    Speaker = gs.Session.Speaker,
                    Status = gs.Status,
                })
                .ToListAsync(ct);

            detail.Flights = await _unitOfWork.Flights.QueryNoTracking()
                .Where(f => f.GuestId == guest.Id)
                .Select(f => new GuestOverviewFlightRow
                {
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
                .ToListAsync(ct);

            detail.Accommodations = await _unitOfWork.Accommodations.QueryNoTracking()
                .Where(a => a.GuestId == guest.Id)
                .Select(a => new GuestOverviewAccommodationRow
                {
                    Id = a.PublicId,
                    Hotel = a.Hotel != null ? a.Hotel.Name : null,
                    HotelImageUrl = a.Hotel != null ? a.Hotel.ImageUrl : null,
                    RoomType = a.RoomType != null ? a.RoomType.Name : null,
                    CheckIn = a.CheckIn,
                    CheckOut = a.CheckOut,
                    ImageUrl = a.ImageUrl,
                })
                .ToListAsync(ct);

            detail.Transport = await _unitOfWork.Transports.QueryNoTracking()
                .Where(t => t.GuestId == guest.Id)
                .Select(t => new GuestOverviewTransportRow
                {
                    Id = t.PublicId,
                    TripStatus = t.TripStatus,
                    Vehicle = t.Vehicle != null ? $"{t.Vehicle.VehicleNumber} · {t.Vehicle.VehicleModel}" : null,
                    DriverName = t.Driver != null && t.Driver.User != null ? $"{t.Driver.User.FirstName} {t.Driver.User.LastName}".Trim() : null,
                    Pickup = t.PickupLocation != null ? t.PickupLocation.Address : null,
                    Dropoff = t.DropoffLocation != null ? t.DropoffLocation.Address : null,
                    PickupTime = t.PickupTime,
                    DropoffTime = t.DropoffTime,
                })
                .ToListAsync(ct);

            var seatResult = await _seatingService.GetGuestSeatAssignmentsAsync(guestId, ct);
            detail.Seatings = (seatResult.Data ?? new List<GuestSeatAssignmentDto>())
                .Select(s => new GuestOverviewSeatRow
                {
                    EventTitle = s.EventTitle,
                    SessionTitle = s.SessionTitle,
                    SeatCode = s.SeatCode,
                })
                .ToList();

            // Flight/Accommodation/Transport are system slots on this same plan —
            // already covered by the three sections above, so they're excluded here.
            var planResult = await _serviceCatalogService.GetGuestServicePlanAsync(guestId, ct);
            detail.OtherServices = (planResult.Data?.Slots ?? new())
                .Where(s => !s.IsSystem)
                .ToList();

            return ApiResponse<GuestOverviewDetailResponse>.SuccessResponse(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest overview detail for guest {GuestId}", guestId);
            return ApiResponse<GuestOverviewDetailResponse>.ServerErrorResponse("An error occurred while retrieving guest detail");
        }
    }
}
