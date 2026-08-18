using Core.Common.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Meeting;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class MeetingService(IUnitOfWork _unitOfWork, ILogger<MeetingService> _logger, ICurrentUser _currentUser) : IMeetingService
    {
        public async Task<ApiResponse<GetMeetingResponse>> CreateMeetingAsync(CreateMeetingRequest request, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return ApiResponse<GetMeetingResponse>.ErrorResponse("Meeting name is required.");

                // Event arrives as a public Guid — resolve to its internal id for the FK.
                var eventEntity = await _unitOfWork.Events.Query()
                    .FirstOrDefaultAsync(e => e.PublicId == request.EventId, ct);
                if (eventEntity == null)
                    return ApiResponse<GetMeetingResponse>.NotFoundResponse("Event not found.");

                var requestedIds = (request.EventGuestIds ?? new List<Guid>()).Distinct().ToList();

                var attendees = new List<EventGuest>();
                if (requestedIds.Count > 0)
                {
                    var (resolved, attendeeError) = await ResolveAttendeesAsync(requestedIds, eventEntity.Id, ct);
                    if (attendeeError != null)
                        return ApiResponse<GetMeetingResponse>.ErrorResponse(attendeeError);
                    attendees = resolved;

                    // First check a meeting doesn't already exist for one of these
                    // attendees at an overlapping date/time — nobody can be in two
                    // meetings at once.
                    var conflictingMeeting = await _unitOfWork.Meetings.Query()
                        .Where(m => m.IsDeleted != true && m.Date == request.Date)
                        .Where(m => m.EventGuests.Any(eg => requestedIds.Contains(eg.PublicId)))
                        .Where(m => request.StartTime == null || request.EndTime == null
                                 || m.StartTime == null || m.EndTime == null
                                 || (m.StartTime < request.EndTime && request.StartTime < m.EndTime))
                        .FirstOrDefaultAsync(ct);

                    if (conflictingMeeting != null)
                        return ApiResponse<GetMeetingResponse>.ConflictResponse(
                            "One or more guests already have a meeting scheduled at this date/time.");
                }

                var meeting = new Meeting
                {
                    Name = request.Name,
                    Date = request.Date,
                    Location = request.Location,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    MeetingAgenda = request.MeetingAgenda,
                    EventGuests = attendees,
                    EventId = eventEntity.Id
                };
                meeting.SetCreationAudit(_currentUser.UserId);

                await _unitOfWork.Meetings.AddAsync(meeting, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var response = new GetMeetingResponse
                {
                    Id = meeting.PublicId,
                    Name = meeting.Name,
                    Date = meeting.Date,
                    Location = meeting.Location,
                    StartTime = meeting.StartTime,
                    EndTime = meeting.EndTime,
                    MeetingAgenda = meeting.MeetingAgenda,
                    EventId = request.EventId,
                    Guests = attendees.Select(MapAttendee).ToList(),
                };

                return ApiResponse<GetMeetingResponse>.SuccessResponse(response, "Meeting created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating meeting");
                return ApiResponse<GetMeetingResponse>.ServerErrorResponse("An error occurred while creating the meeting.");
            }
        }

        public async Task<ApiResponse<List<GetMeetingResponse>>> GetMeetingAllMeetingsAsync(Guid eventId, CancellationToken ct)
        {
            try
            {
                if (eventId == Guid.Empty)
                    return ApiResponse<List<GetMeetingResponse>>.ErrorResponse("Event id is required.");

                // Resolve the event's public id to its internal id; no event -> no meetings.
                var eventEntity = await _unitOfWork.Events.Query()
                    .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
                if (eventEntity == null)
                    return ApiResponse<List<GetMeetingResponse>>.SuccessResponse(new List<GetMeetingResponse>());

                var meetings = await _unitOfWork.Meetings.Query()
                    .Include(m => m.EventGuests).ThenInclude(eg => eg.Guest)
                    .Where(m => m.EventId == eventEntity.Id && m.IsDeleted != true)
                    .OrderBy(m => m.Date).ThenBy(m => m.StartTime)
                    .ToListAsync(ct);

                var response = meetings.Select(m => new GetMeetingResponse
                {
                    Id = m.PublicId,
                    Name = m.Name,
                    Date = m.Date,
                    Location = m.Location,
                    StartTime = m.StartTime,
                    EndTime = m.EndTime,
                    MeetingAgenda = m.MeetingAgenda,
                    EventId = eventId,
                    Guests = m.EventGuests.Select(MapAttendee).ToList(),
                }).ToList();

                return ApiResponse<List<GetMeetingResponse>>.SuccessResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching meetings for event {EventId}", eventId);
                return ApiResponse<List<GetMeetingResponse>>.ServerErrorResponse("An error occurred while fetching meetings.");
            }
        }
        public async Task<ApiResponse<GetMeetingResponse>> EditMeetingAsync(EditMeetingRequest request, CancellationToken ct)
        {
            try
            {
                if (request.MeetId == Guid.Empty)
                    return ApiResponse<GetMeetingResponse>.ErrorResponse("Meeting id is required.");

                // Resolve the event scope (public id -> internal id). No such event means
                // no meeting can match that scope.
                var eventEntity = await _unitOfWork.Events.Query()
                    .FirstOrDefaultAsync(e => e.PublicId == request.EventId, ct);
                if (eventEntity == null)
                    return ApiResponse<GetMeetingResponse>.NotFoundResponse("Meeting not found.");

                var meeting = await _unitOfWork.Meetings.Query()
                    .Include(m => m.EventGuests).ThenInclude(eg => eg.Guest)
                    .FirstOrDefaultAsync(m => m.PublicId == request.MeetId && m.EventId == eventEntity.Id, ct);

                if (meeting == null || meeting.IsDeleted == true)
                    return ApiResponse<GetMeetingResponse>.NotFoundResponse("Meeting not found.");

                // Every field is optional (partial update) — null means "leave as is".
                if (request.Name != null)
                {
                    if (string.IsNullOrWhiteSpace(request.Name))
                        return ApiResponse<GetMeetingResponse>.ErrorResponse("Meeting name cannot be empty.");
                    meeting.Name = request.Name;
                }
                if (request.Location != null) meeting.Location = request.Location;
                if (request.StartTime.HasValue) meeting.StartTime = request.StartTime;
                if (request.EndTime.HasValue) meeting.EndTime = request.EndTime;
                if (request.Agenda != null) meeting.MeetingAgenda = request.Agenda;

                // EventGuestIds omitted (null) leaves attendees untouched; an explicit
                // empty list clears them — the two are meaningfully different.
                if (request.EventGuestIds != null)
                {
                    var requestedIds = request.EventGuestIds.Distinct().ToList();

                    var (attendees, attendeeError) = requestedIds.Count == 0
                        ? (new List<EventGuest>(), null)
                        : await ResolveAttendeesAsync(requestedIds, meeting.EventId, ct);
                    if (attendeeError != null)
                        return ApiResponse<GetMeetingResponse>.ErrorResponse(attendeeError);

                    if (requestedIds.Count > 0)
                    {
                        // Same double-booking check as CreateMeetingAsync, excluding this
                        // meeting itself so re-saving its own existing guests isn't a "conflict".
                        var conflictingMeeting = await _unitOfWork.Meetings.Query()
                            .Where(m => m.Id != meeting.Id && m.IsDeleted != true && m.Date == meeting.Date)
                            .Where(m => m.EventGuests.Any(eg => requestedIds.Contains(eg.PublicId)))
                            .Where(m => meeting.StartTime == null || meeting.EndTime == null
                                     || m.StartTime == null || m.EndTime == null
                                     || (m.StartTime < meeting.EndTime && meeting.StartTime < m.EndTime))
                            .FirstOrDefaultAsync(ct);

                        if (conflictingMeeting != null)
                            return ApiResponse<GetMeetingResponse>.ConflictResponse(
                                "One or more guests already have a meeting scheduled at this date/time.");
                    }

                    meeting.EventGuests.Clear();
                    foreach (var eg in attendees) meeting.EventGuests.Add(eg);
                }

                meeting.SetUpdateAudit(_currentUser.UserId);
                await _unitOfWork.SaveChangesAsync(ct);

                var response = new GetMeetingResponse
                {
                    Id = meeting.PublicId,
                    Name = meeting.Name,
                    Date = meeting.Date,
                    Location = meeting.Location,
                    StartTime = meeting.StartTime,
                    EndTime = meeting.EndTime,
                    MeetingAgenda = meeting.MeetingAgenda,
                    EventId = request.EventId,
                    Guests = meeting.EventGuests.Select(MapAttendee).ToList(),
                };

                return ApiResponse<GetMeetingResponse>.SuccessResponse(response, "Meeting updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating meeting {MeetingId}", request.MeetId);
                return ApiResponse<GetMeetingResponse>.ServerErrorResponse("Error updating meeting.");
            }
        }

        /// <summary>
        /// Turns the requested EventGuest public ids into rows, refusing any that
        /// belong to a different event.
        /// </summary>
        /// <remarks>
        /// A meeting is an event-scoped thing, so an attendee from another event is
        /// a client bug worth naming rather than a silently dropped row — and
        /// letting one through would put a guest in a meeting for an event they
        /// aren't on.
        /// </remarks>
        private async Task<(List<EventGuest> attendees, string error)> ResolveAttendeesAsync(
            List<Guid> eventGuestPublicIds, int eventId, CancellationToken ct)
        {
            var attendees = await _unitOfWork.EventGuests.Query()
                .Include(eg => eg.Guest)
                .Where(eg => eventGuestPublicIds.Contains(eg.PublicId))
                .ToListAsync(ct);

            if (attendees.Count != eventGuestPublicIds.Count)
                return (null, "One or more guests were not found.");

            if (attendees.Any(eg => eg.EventId != eventId))
                return (null, "One or more guests are not on this meeting's event.");

            return (attendees, null);
        }

        private static GuestInfo MapAttendee(EventGuest eg) => new()
        {
            Id = eg.PublicId,
            PersonId = eg.Guest?.PublicId,
            Name = $"{eg.Guest?.FirstName} {eg.Guest?.LastName}".Trim(),
            Email = eg.Guest?.Email,
            PhotoUrl = eg.Guest?.PhotoUrl,
        };
    }
}
