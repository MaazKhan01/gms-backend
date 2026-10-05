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

                // The dashboard counts PARTICIPATIONS, not people: "120 guests" on
                // this event means 120 EventGuest rows, and the same person on a
                // second event is that event's number, not this one's.
                var guests = await _unitOfWork.EventGuests.Query()
                    .Where(eg => eg.EventId == ev.Id)
                    .Include(eg => eg.Guest)
                    .Include(eg => eg.ServiceLevel)
                    .ToListAsync(ct);

                var meetings = await _unitOfWork.Meetings.Query()
                    .Where(m => m.EventId == ev.Id && m.IsDeleted != true)
                    .OrderBy(m => m.Date).ThenBy(m => m.StartTime)
                    .ToListAsync(ct);

                // Invitation/accreditation status + travel moved to their own tables.
                var guestIds = guests.Select(g => g.Id).ToList();
                var invitations = (await _unitOfWork.Invitations.Query()
                        .Where(i => guestIds.Contains(i.EventGuestId)).ToListAsync(ct))
                    .GroupBy(i => i.EventGuestId)
                    .ToDictionary(gr => gr.Key, gr => gr.First());
                // Travel dates come from the flight bookings — EventGuest no longer
                // carries its own arrival/departure. Pulled with their times so the
                // movement chart below can be built from the same fetch.
                var flights = await _unitOfWork.Flights.Query()
                    .Where(f => guestIds.Contains(f.EventGuestId))
                    .Select(f => new { f.EventGuestId, f.ArrivalTime, f.DepartureTime })
                    .ToListAsync(ct);
                var flightGuestIds = flights.Select(f => f.EventGuestId).Distinct().ToHashSet();
                var accommodationGuestIds = (await _unitOfWork.Accommodations.Query()
                    .Where(a => guestIds.Contains(a.EventGuestId)).Select(a => a.EventGuestId).Distinct().ToListAsync(ct)).ToHashSet();
                var transportGuestIds = (await _unitOfWork.Transports.Query()
                    .Where(t => guestIds.Contains(t.EventGuestId)).Select(t => t.EventGuestId).Distinct().ToListAsync(ct)).ToHashSet();
                var seatedGuestIds = (await _unitOfWork.SeatAssigns.Query()
                    .Where(sa => guestIds.Contains(sa.EventGuestId)).Select(sa => sa.EventGuestId).Distinct().ToListAsync(ct)).ToHashSet();

                var sessionGuestCounts = (await _unitOfWork.GuestSessions.Query()
                        .Where(gs => guestIds.Contains(gs.EventGuestId))
                        .GroupBy(gs => gs.SessionId)
                        .Select(gr => new { SessionId = gr.Key, Count = gr.Count() })
                        .ToListAsync(ct))
                    .ToDictionary(x => x.SessionId, x => x.Count);

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
                            ImageUrl = s.ImageUrl,
                            GuestCount = sessionGuestCounts.TryGetValue(s.Id, out var gc) ? gc : 0,
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
                            Name = $"{g.Guest.FirstName} {g.Guest.LastName}".Trim(),
                            Email = g.Guest.Email,
                            PhotoUrl = g.Guest.PhotoUrl,
                            Organization = g.Organization,
                            ServiceLevelName = g.ServiceLevel?.Name,
                            ServiceLevelColor = g.ServiceLevel?.Color,
                            InvitationStatus = invitations.TryGetValue(g.Id, out var inv) ? inv.InvitationStatus : null,
                        })
                        .ToList(),
                };

                // -- Analytics -------------------------------------------------
                Invitation InvOf(int eventGuestId) => invitations.TryGetValue(eventGuestId, out var i) ? i : null;

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
                    guests.Where(g => g.Guest.NationalityId != null)
                          .GroupBy(g => g.Guest.NationalityId.Value)
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
                // One arrival and one departure per PERSON, not per flight: a return
                // booking would otherwise count its own two legs as two arrivals.
                // Earliest landing is the arrival, latest take-off the departure.
                var movement = new SortedDictionary<DateOnly, DashboardDayCountDto>();
                foreach (var gr in flights.GroupBy(f => f.EventGuestId))
                {
                    // Min/Max over DateTime? skip nulls and yield null when all are.
                    var arrival = gr.Min(f => f.ArrivalTime);
                    var departure = gr.Max(f => f.DepartureTime);

                    if (arrival.HasValue)
                    {
                        var ad = DateOnly.FromDateTime(arrival.Value);
                        if (!movement.ContainsKey(ad)) movement[ad] = new DashboardDayCountDto { Date = ad };
                        movement[ad].Arrivals++;
                    }
                    if (departure.HasValue)
                    {
                        var dd = DateOnly.FromDateTime(departure.Value);
                        if (!movement.ContainsKey(dd)) movement[dd] = new DashboardDayCountDto { Date = dd };
                        movement[dd].Departures++;
                    }
                }
                response.Movements = movement.Values.ToList();

                response.Journey = await BuildJourneyAsync(
                    ev, guests, flightGuestIds, accommodationGuestIds, transportGuestIds, ct);

                return ApiResponse<GetDashboardResponse>.SuccessResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building dashboard for event {EventId}", eventId);
                return ApiResponse<GetDashboardResponse>.ServerErrorResponse("An error occurred while loading the dashboard.");
            }
        }

        /// <summary>
        /// The mission as one sequence. Each step's numbers come from the same
        /// rows its own screen counts, so the strip and the screen can never
        /// disagree — the booking sets are passed in rather than re-queried.
        /// </summary>
        private async Task<MissionJourneyDto> BuildJourneyAsync(
            Event ev,
            List<EventGuest> roster,
            HashSet<int> withFlight,
            HashSet<int> withHotel,
            HashSet<int> withTransport,
            CancellationToken ct)
        {
            var total = roster.Count;
            int Pct(int n, int of) => of > 0 ? (int)Math.Round(n * 100.0 / of) : 0;

            var journey = new MissionJourneyDto
            {
                DelegationCap = ev.DelegationCap,
                RosterCount = total,
                HostName = ev.HostName,
            };

            // ── Phase 1: the mission exists, so this one is done by definition ──
            journey.Steps.Add(new MissionJourneyStepDto
            {
                Phase = 1, Code = "events",
                Title = "Invitation & Mission Creation", TitleAr = "الدعوة وإنشاء المهمة",
                Percent = 100,
                Stat = ev.HostInvitationId.HasValue ? "From invitation" : "Direct",
            });

            // ── Phase 2: roster against the host's cap, HR sign-off as the flag ──
            var pendingHr = roster.Count(g => g.HrVerificationStatus == HrVerificationStatuses.Pending);
            var rejectedHr = roster.Count(g => g.HrVerificationStatus == HrVerificationStatuses.Rejected);
            journey.Steps.Add(new MissionJourneyStepDto
            {
                Phase = 2, Code = "nominations",
                Title = "Delegation Assembly", TitleAr = "تشكيل الوفد",
                // No cap means no target to measure against, so the bar tracks HR
                // sign-off instead of inventing a denominator.
                Percent = ev.DelegationCap.HasValue && ev.DelegationCap > 0
                    ? Math.Min(100, Pct(total, ev.DelegationCap.Value))
                    : Pct(total - pendingHr - rejectedHr, total),
                Stat = ev.DelegationCap.HasValue ? $"{total}/{ev.DelegationCap} roster" : $"{total} nominated",
                Flag = rejectedHr > 0 ? $"{rejectedHr} HR-rejected"
                     : pendingHr > 0 ? $"{pendingHr} awaiting HR"
                     : null,
            });

            // ── Phase 4: the nomination letter's own status is the progress ──────
            var letter = await _unitOfWork.NominationLetters.QueryNoTracking()
                .FirstOrDefaultAsync(l => l.EventId == ev.Id, ct);
            var letterStatus = letter?.Status ?? NominationLetterStatuses.NotGenerated;
            journey.Steps.Add(new MissionJourneyStepDto
            {
                Phase = 4, Code = "nomination-letter",
                Title = "Host Communication", TitleAr = "التواصل مع المضيف",
                Percent = letterStatus switch
                {
                    NominationLetterStatuses.Acknowledged => 100,
                    NominationLetterStatuses.Sent => 55,
                    NominationLetterStatuses.ChangesRequested => 55,
                    NominationLetterStatuses.Draft => 20,
                    _ => 0,
                },
                Stat = letterStatus.Replace('_', ' '),
                Flag = letterStatus == NominationLetterStatuses.ChangesRequested ? "Changes requested" : null,
            });

            // ── Phase 5: three bookable slots per delegate ───────────────────────
            var booked = roster.Count(g => withFlight.Contains(g.Id))
                       + roster.Count(g => withHotel.Contains(g.Id))
                       + roster.Count(g => withTransport.Contains(g.Id));
            var visaOpen = roster.Count(g => g.VisaRequired && g.VisaStatus != VisaStatuses.Active
                                                            && g.VisaStatus != VisaStatuses.ExpiringSoon);
            journey.Steps.Add(new MissionJourneyStepDto
            {
                Phase = 5, Code = "services",
                Title = "Travel & Logistics", TitleAr = "السفر والخدمات اللوجستية",
                Percent = Pct(booked, total * 3),
                Stat = $"{Pct(booked, total * 3)}% booked",
                Flag = visaOpen > 0 ? $"{visaOpen} visa{(visaOpen == 1 ? "" : "s")} outstanding" : null,
            });

            // ── Phase 6: the readiness gate, derived the same way Readiness does ─
            var travelDate = ev.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var waived = (await _unitOfWork.ReadinessWaivers.QueryNoTracking()
                    .Where(w => roster.Select(r => r.Id).Contains(w.EventGuestId))
                    .Select(w => new { w.EventGuestId, w.ItemKey })
                    .ToListAsync(ct))
                .GroupBy(w => w.EventGuestId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ItemKey).ToHashSet());

            bool Ready(EventGuest g)
            {
                var ok = waived.TryGetValue(g.Id, out var w) ? w : new HashSet<string>();
                bool Met(string key, bool met) => met || ok.Contains(key);
                return Met(ReadinessItems.Passport,
                           !string.IsNullOrWhiteSpace(g.Guest?.PassportNumber)
                           && g.Guest.PassportExpiry.HasValue && g.Guest.PassportExpiry >= travelDate)
                    && Met(ReadinessItems.Visa,
                           !g.VisaRequired || g.VisaStatus == VisaStatuses.Active || g.VisaStatus == VisaStatuses.ExpiringSoon)
                    && Met(ReadinessItems.Flight, withFlight.Contains(g.Id))
                    && Met(ReadinessItems.Accommodation, withHotel.Contains(g.Id))
                    && Met(ReadinessItems.Transport, withTransport.Contains(g.Id));
            }

            var ready = roster.Count(Ready);
            journey.Steps.Add(new MissionJourneyStepDto
            {
                Phase = 6, Code = "readiness",
                Title = "Pre-Departure Readiness", TitleAr = "الجاهزية قبل السفر",
                Percent = Pct(ready, total),
                Stat = $"{ready}/{total} travel ready",
                Flag = total > 0 && ready < total ? $"{total - ready} not ready" : null,
            });

            // ── Phase 7: on the ground. Open incidents are the whole signal ──────
            var incidents = await _unitOfWork.Incidents.QueryNoTracking()
                .Where(i => i.EventId == ev.Id)
                .Select(i => i.Status)
                .ToListAsync(ct);
            var openIncidents = incidents.Count(s => !IncidentStatuses.IsClosed(s));
            journey.Steps.Add(new MissionJourneyStepDto
            {
                Phase = 7, Code = "on-mission-ops",
                Title = "Active Mission Ops", TitleAr = "عمليات المهمة",
                Percent = incidents.Count == 0 ? 0 : Pct(incidents.Count - openIncidents, incidents.Count),
                Stat = incidents.Count == 0 ? "No incidents" : $"{incidents.Count - openIncidents}/{incidents.Count} resolved",
                Flag = openIncidents > 0 ? $"{openIncidents} open" : null,
            });

            // ── Phase 9: reports in, then the report out ─────────────────────────
            //
            // Two things have to happen and they are not the same size, so the
            // bar is not a simple ratio: collecting every delegate's report is
            // most of the work, and the combined report's four sign-offs are the
            // rest. Weighted 60/40 so the step moves while reports come in
            // rather than sitting at zero until the last one lands.
            var reportStatuses = await _unitOfWork.PostMissionReports.QueryNoTracking()
                .Where(r => roster.Select(x => x.Id).Contains(r.EventGuestId))
                .Select(r => r.Status)
                .ToListAsync(ct);

            var reportsIn = reportStatuses.Count(st => st == PostMissionReportStatuses.Submitted
                                                    || st == PostMissionReportStatuses.Approved);

            var combined = await _unitOfWork.CombinedReports.QueryNoTracking()
                .Where(c => c.EventId == ev.Id)
                .Select(c => c.Status)
                .FirstOrDefaultAsync(ct);

            // How far the combined report itself has got, as a fraction of its
            // own four steps.
            var combinedPct = combined switch
            {
                CombinedReportStatuses.Draft => 25,
                CombinedReportStatuses.InReview => 50,
                CombinedReportStatuses.Approved => 75,
                CombinedReportStatuses.Published => 100,
                _ => 0,
            };

            var closed = ev.Status == MissionStatuses.Closed;
            var reportsPct = total == 0 ? 0 : Pct(reportsIn, total);
            var phase9 = closed ? 100 : (int)Math.Round(reportsPct * 0.6 + combinedPct * 0.4);

            var outstanding = Math.Max(0, total - reportsIn);
            journey.Steps.Add(new MissionJourneyStepDto
            {
                Phase = 9, Code = "combined-report",
                Title = "Reports & Close", TitleAr = "التقارير والإغلاق",
                Percent = phase9,
                Stat = closed
                    ? "Mission closed"
                    : combined == CombinedReportStatuses.Published
                        ? "Report published"
                        : $"{reportsIn}/{total} reports in",
                // Only worth flagging once the mission is over — before that,
                // "nobody has reported" is simply the truth and not a problem.
                Flag = !closed && ev.EndDate.HasValue && ev.EndDate.Value < DateOnly.FromDateTime(DateTime.UtcNow) && outstanding > 0
                    ? $"{outstanding} report{(outstanding == 1 ? "" : "s")} outstanding"
                    : null,
            });

            return journey;
        }
    }
}
