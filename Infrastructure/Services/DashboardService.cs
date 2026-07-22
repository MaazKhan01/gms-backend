using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class DashboardService(IUnitOfWork _unitOfWork, ILogger<DashboardService> _logger) : IDashboardService
    {
        // Cap on "Recent guests" — most-recently-added first.
        private const int RecentGuestsLimit = 8;

        public async Task<ApiResponse<GetDashboardResponse>> GetDashboardAsync(Guid eventId, CancellationToken ct)
        {
            try
            {
                if (eventId == Guid.Empty)
                    return ApiResponse<GetDashboardResponse>.ErrorResponse("Event id is required.");

                var ev = await _unitOfWork.Events.Query()
                    .Include(e => e.Sessions)
                    .FirstOrDefaultAsync(e => e.Id == eventId, ct);

                if (ev == null)
                    return ApiResponse<GetDashboardResponse>.NotFoundResponse("Event not found.");

                var guests = await _unitOfWork.Guests.Query()
                    .Where(g => g.EventId == eventId)
                    .ToListAsync(ct);

                var meetings = await _unitOfWork.Meetings.Query()
                    .Where(m => m.EventId == eventId && m.IsDeleted != true)
                    .OrderBy(m => m.Date).ThenBy(m => m.StartTime)
                    .ToListAsync(ct);

                var response = new GetDashboardResponse
                {
                    Id = ev.Id,
                    Title = ev.Title,
                    Venue = ev.VenueName,
                    StartDate = ev.StartDate,
                    EndDate = ev.EndDate,

                    Sessions = ev.Sessions
                        .OrderBy(s => s.Date).ThenBy(s => s.Time)
                        .Select(s => new DashboardSessionDto
                        {
                            Id = s.Id,
                            Title = s.Title,
                            Date = s.Date,
                            Time = s.Time,
                            Room = s.Room,
                        })
                        .ToList(),

                    FunnelData = new DashboardFunnelDto
                    {
                        TotalGuests = guests.Count,
                        ConfirmedGuest = guests.Count(g => g.InvitationStatus == GuestInvitationStatus.Accepted),
                        AwaitingGuest = guests.Count(g => g.InvitationStatus == GuestInvitationStatus.Sent
                                                        || g.InvitationStatus == GuestInvitationStatus.Opened),
                        TravelBooked = guests.Count(g => !string.IsNullOrWhiteSpace(g.FlightNumber)
                                                       || !string.IsNullOrWhiteSpace(g.Hotel)
                                                       || g.SeatId != null),
                        AccreditationIssued = guests.Count(g => g.AccreditationStatus == GuestAccreditationStatus.Issued),
                    },

                    Meetings = meetings.Select(m => new DashboardMeetingDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Date = m.Date,
                        StartTime = m.StartTime,
                        EndTime = m.EndTime,
                        Location = m.Location,
                    }).ToList(),

                    RecentGuests = guests
                        .OrderByDescending(g => g.CreatedAt)
                        .Take(RecentGuestsLimit)
                        .Select(g => new DashboardGuestDto
                        {
                            Id = g.Id,
                            Name = $"{g.FirstName} {g.LastName}".Trim(),
                            Organization = g.Organization,
                            Tier = g.Tier,
                            InvitationStatus = g.InvitationStatus,
                            ArrivalDate = g.ArrivalDate,
                        })
                        .ToList(),
                };

                return ApiResponse<GetDashboardResponse>.SuccessResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building dashboard for event {EventId}", eventId);
                return ApiResponse<GetDashboardResponse>.ServerErrorResponse("An error occurred while loading the dashboard.");
            }
        }
    }
}
