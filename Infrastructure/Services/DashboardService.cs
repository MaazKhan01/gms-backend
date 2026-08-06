using System;
using System.Collections.Generic;
using System.Linq;
using DomainPersistence.Entities;
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

        // Breakdown charts get noisy past a handful of slices; the tail is
        // rolled into an "Other" row so the totals still add up.
        private const int BreakdownLimit = 6;

        /// <summary>
        /// Keeps the largest <see cref="BreakdownLimit"/> slices and folds the tail
        /// into a single "Other" row, so a chart of 40 nationalities stays readable
        /// without misrepresenting the total.
        /// </summary>
        private static List<DashboardBreakdownDto> TopBreakdown(IEnumerable<DashboardBreakdownDto> all)
        {
            var ordered = all.OrderByDescending(b => b.Count).ToList();
            if (ordered.Count <= BreakdownLimit) return ordered;

            var top = ordered.Take(BreakdownLimit).ToList();
            top.Add(new DashboardBreakdownDto
            {
                Label = "Other",
                LabelAr = "أخرى",
                Count = ordered.Skip(BreakdownLimit).Sum(b => b.Count),
            });
            return top;
        }

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
                var transportGuestIds = (await _unitOfWork.Transports.Query()
                    .Where(t => guestIds.Contains(t.GuestId)).Select(t => t.GuestId).Distinct().ToListAsync(ct)).ToHashSet();
                var seatedGuestIds = (await _unitOfWork.SeatAssigns.Query()
                    .Where(sa => guestIds.Contains(sa.GuestId)).Select(sa => sa.GuestId).Distinct().ToListAsync(ct)).ToHashSet();

                // Lookups for the breakdown charts, pulled as dictionaries rather
                // than joined per guest so a list of any size costs three queries.
                // Levels are global in v2, so the breakdown lists every level and
                // drops the ones with no guests in this event further down.
                var serviceLevels = await _unitOfWork.ServiceLevels.Query()
                    .OrderBy(sl => sl.SortOrder).ToListAsync(ct);
                var nationalityNames = await _unitOfWork.Nationalities.Query()
                    .ToDictionaryAsync(n => n.Id, n => new { n.Name, n.NameAr }, ct);
                var organizationNames = await _unitOfWork.Organizations.Query()
                    .ToDictionaryAsync(o => o.Id, o => new { o.Name, o.NameAr }, ct);

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

                // -- Analytics -------------------------------------------------
                Invitation InvOf(int guestId) => invitations.TryGetValue(guestId, out var i) ? i : null;

                var invited = guests.Count(g => InvOf(g.Id) != null
                                             && InvOf(g.Id).InvitationStatus != GuestInvitationStatus.NotSent);
                var accepted = invitations.Values.Count(i => i.InvitationStatus == GuestInvitationStatus.Accepted);
                var declined = invitations.Values.Count(i => i.InvitationStatus == GuestInvitationStatus.Declined);

                response.Rsvp = new DashboardRsvpDto
                {
                    Accepted = accepted,
                    Declined = declined,
                    Awaiting = invitations.Values.Count(i => i.InvitationStatus == GuestInvitationStatus.Sent
                                                          || i.InvitationStatus == GuestInvitationStatus.Opened),
                    // A guest with no invitation row has had nothing sent either.
                    NotSent = guests.Count - invited,
                    ResponseRate = invited == 0 ? 0 : (int)Math.Round(100.0 * (accepted + declined) / invited),
                };

                response.Accreditation = new DashboardAccreditationDto
                {
                    Issued = guests.Count(g => g.AccreditationRequired
                                            && InvOf(g.Id) != null
                                            && InvOf(g.Id).AccreditationStatus == GuestAccreditationStatus.Issued),
                    Revoked = guests.Count(g => g.AccreditationRequired
                                             && InvOf(g.Id) != null
                                             && InvOf(g.Id).AccreditationStatus == GuestAccreditationStatus.Revoked),
                    NotRequired = guests.Count(g => !g.AccreditationRequired),
                };
                // Whatever is required but neither issued nor revoked is pending.
                response.Accreditation.Pending = guests.Count(g => g.AccreditationRequired)
                    - response.Accreditation.Issued - response.Accreditation.Revoked;

                response.Travel = new DashboardTravelDto
                {
                    FlightsBooked = flightGuestIds.Count,
                    AccommodationBooked = accommodationGuestIds.Count,
                    TransportBooked = transportGuestIds.Count,
                    GuestsWithTravel = guests.Count(g => flightGuestIds.Contains(g.Id)
                                                      || accommodationGuestIds.Contains(g.Id)
                                                      || transportGuestIds.Contains(g.Id)),
                };

                response.Seating = new DashboardSeatingDto
                {
                    Assigned = seatedGuestIds.Count,
                    Unassigned = guests.Count - seatedGuestIds.Count,
                };

                response.ServiceLevels = serviceLevels
                    .Select(sl => new DashboardBreakdownDto
                    {
                        Label = sl.Name,
                        LabelAr = sl.NameAr,
                        Color = sl.Color,
                        Count = guests.Count(g => g.ServiceLevelId == sl.Id),
                    })
                    .Where(b => b.Count > 0)
                    .ToList();

                // Guests on no level are still part of the event, so they get a
                // slice rather than vanishing from a chart meant to account for
                // the whole list.
                var unlevelled = guests.Count(g => g.ServiceLevelId == null);
                if (unlevelled > 0)
                {
                    response.ServiceLevels.Add(new DashboardBreakdownDto
                    {
                        Label = "No level",
                        LabelAr = "بدون فئة",
                        Count = unlevelled,
                    });
                }

                response.Nationalities = TopBreakdown(
                    guests.Where(g => g.NationalityId != null)
                          .GroupBy(g => g.NationalityId.Value)
                          .Select(gr => new DashboardBreakdownDto
                          {
                              Label = nationalityNames.ContainsKey(gr.Key) ? nationalityNames[gr.Key].Name : "Unknown",
                              LabelAr = nationalityNames.ContainsKey(gr.Key) ? nationalityNames[gr.Key].NameAr : null,
                              Count = gr.Count(),
                          }));

                response.Organizations = TopBreakdown(
                    guests.Where(g => g.OrganizationId != null)
                          .GroupBy(g => g.OrganizationId.Value)
                          .Select(gr => new DashboardBreakdownDto
                          {
                              Label = organizationNames.ContainsKey(gr.Key) ? organizationNames[gr.Key].Name : "Unknown",
                              LabelAr = organizationNames.ContainsKey(gr.Key) ? organizationNames[gr.Key].NameAr : null,
                              Count = gr.Count(),
                          }));

                // One row per day that has any movement, so the chart carries no
                // empty leading or trailing tail.
                var movement = new SortedDictionary<DateOnly, DashboardDayCountDto>();
                foreach (var g in guests)
                {
                    if (g.ArrivalDate.HasValue)
                    {
                        var ad = g.ArrivalDate.Value;
                        if (!movement.ContainsKey(ad)) movement[ad] = new DashboardDayCountDto { Date = ad };
                        movement[ad].Arrivals++;
                    }
                    if (g.DepartureDate.HasValue)
                    {
                        var dd = g.DepartureDate.Value;
                        if (!movement.ContainsKey(dd)) movement[dd] = new DashboardDayCountDto { Date = dd };
                        movement[dd].Departures++;
                    }
                }
                response.Movements = movement.Values.ToList();

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
