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
                    .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

                if (ev == null)
                    return ApiResponse<GetDashboardResponse>.NotFoundResponse("Event not found.");

                var guests = await _unitOfWork.Guests.Query()
                    .Where(g => g.EventId == ev.Id)
                    .ToListAsync(ct);

                var meetings = await _unitOfWork.Meetings.Query()
                    .Where(m => m.EventId == ev.Id && m.IsDeleted != true)
                    .OrderBy(m => m.Date).ThenBy(m => m.StartTime)
                    .ToListAsync(ct);

                // Invitation/accreditation status + travel moved to their own tables.
                var guestIds = guests.Select(g => g.Id).ToList();
                var invitations = (await _unitOfWork.Invitations.Query()
                        .Where(i => guestIds.Contains(i.GuestId)).ToListAsync(ct))
                    .GroupBy(i => i.GuestId)
                    .ToDictionary(gr => gr.Key, gr => gr.First());
                var flightGuestIds = (await _unitOfWork.Flights.Query()
                    .Where(f => guestIds.Contains(f.GuestId)).Select(f => f.GuestId).Distinct().ToListAsync(ct)).ToHashSet();
                var accommodationGuestIds = (await _unitOfWork.Accommodations.Query()
                    .Where(a => guestIds.Contains(a.GuestId)).Select(a => a.GuestId).Distinct().ToListAsync(ct)).ToHashSet();

                var response = new GetDashboardResponse
                {
                    Id = ev.PublicId,
                    Title = ev.Title,
                    Venue = ev.VenueName,
                    StartDate = ev.StartDate,
                    EndDate = ev.EndDate,

                    Sessions = ev.Sessions
                        .OrderBy(s => s.Date).ThenBy(s => s.Time)
                        .Select(s => new DashboardSessionDto
                        {
                            Id = s.PublicId,
                            Title = s.Title,
                            Date = s.Date,
                            Time = s.Time,
                            Room = s.Room,
                        })
                        .ToList(),

                    FunnelData = new DashboardFunnelDto
                    {
                        TotalGuests = guests.Count,
                        ConfirmedGuest = invitations.Values.Count(i => i.InvitationStatus == GuestInvitationStatus.Accepted),
                        AwaitingGuest = invitations.Values.Count(i => i.InvitationStatus == GuestInvitationStatus.Sent
                                                        || i.InvitationStatus == GuestInvitationStatus.Opened),
                        TravelBooked = guests.Count(g => flightGuestIds.Contains(g.Id)
                                                       || accommodationGuestIds.Contains(g.Id)
                                                       ),
                        AccreditationIssued = invitations.Values.Count(i => i.AccreditationStatus == GuestAccreditationStatus.Issued),
                    },

                    Meetings = meetings.Select(m => new DashboardMeetingDto
                    {
                        Id = m.PublicId,
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
                            Id = g.PublicId,
                            Name = $"{g.FirstName} {g.LastName}".Trim(),
                            Organization = g.Organization,
                            Tier = g.Tier,
                            InvitationStatus = invitations.TryGetValue(g.Id, out var inv) ? inv.InvitationStatus : null,
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
