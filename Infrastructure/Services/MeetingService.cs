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

                var requestedIds = (request.GuestIds ?? new List<Guid>()).Distinct().ToList();

                var guests = new List<Guest>();
                if (requestedIds.Count > 0)
                {
                    guests = await _unitOfWork.Guests.Query()
                        .Where(g => requestedIds.Contains(g.Id))
                        .ToListAsync(ct);

                    if (guests.Count != requestedIds.Count)
                        return ApiResponse<GetMeetingResponse>.NotFoundResponse("One or more guests were not found.");

                    // First check a meeting doesn't already exist for one of these guests
                    // at an overlapping date/time — a guest can't be in two meetings at once.
                    var conflictingMeeting = await _unitOfWork.Meetings.Query()
                        .Where(m => m.IsDeleted != true && m.Date == request.Date)
                        .Where(m => m.Guests.Any(g => requestedIds.Contains(g.Id)))
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
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Date = request.Date,
                    Location = request.Location,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    MeetingAgenda = request.MeetingAgenda,
                    Guests = guests,
                    EventId = request.EventId
                };
                meeting.SetCreationAudit(_currentUser.UserId);

                await _unitOfWork.Meetings.AddAsync(meeting, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var response = new GetMeetingResponse
                {
                    Id = meeting.Id,
                    Name = meeting.Name,
                    Date = meeting.Date,
                    Location = meeting.Location,
                    StartTime = meeting.StartTime,
                    EndTime = meeting.EndTime,
                    MeetingAgenda = meeting.MeetingAgenda,
                    EventId = meeting.EventId,
                    Guests = guests
                        .Select(g => new GuestInfo { Id = g.Id, Name = $"{g.FirstName} {g.LastName}".Trim() })
                        .ToList(),
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

                var meetings = await _unitOfWork.Meetings.Query()
                    .Include(m => m.Guests)
                    .Where(m => m.EventId == eventId && m.IsDeleted != true)
                    .OrderBy(m => m.Date).ThenBy(m => m.StartTime)
                    .ToListAsync(ct);

                var response = meetings.Select(m => new GetMeetingResponse
                {
                    Id = m.Id,
                    Name = m.Name,
                    Date = m.Date,
                    Location = m.Location,
                    StartTime = m.StartTime,
                    EndTime = m.EndTime,
                    MeetingAgenda = m.MeetingAgenda,
                    EventId = m.EventId,
                    Guests = m.Guests
                        .Select(g => new GuestInfo { Id = g.Id, Name = $"{g.FirstName} {g.LastName}".Trim() })
                        .ToList(),
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

                var meeting = await _unitOfWork.Meetings.Query()
                    .Include(m => m.Guests)
                    .FirstOrDefaultAsync(m => m.Id == request.MeetId && m.EventId == request.EventId, ct);

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

                // GuestIds omitted (null) leaves attendees untouched; an explicit empty
                // list clears them — the two are meaningfully different.
                if (request.GuestIds != null)
                {
                    var requestedIds = request.GuestIds.Distinct().ToList();

                    var guests = requestedIds.Count == 0
                        ? new List<Guest>()
                        : await _unitOfWork.Guests.Query().Where(g => requestedIds.Contains(g.Id)).ToListAsync(ct);

                    if (guests.Count != requestedIds.Count)
                        return ApiResponse<GetMeetingResponse>.NotFoundResponse("One or more guests were not found.");

                    if (requestedIds.Count > 0)
                    {
                        // Same double-booking check as CreateMeetingAsync, excluding this
                        // meeting itself so re-saving its own existing guests isn't a "conflict".
                        var conflictingMeeting = await _unitOfWork.Meetings.Query()
                            .Where(m => m.Id != meeting.Id && m.IsDeleted != true && m.Date == meeting.Date)
                            .Where(m => m.Guests.Any(g => requestedIds.Contains(g.Id)))
                            .Where(m => meeting.StartTime == null || meeting.EndTime == null
                                     || m.StartTime == null || m.EndTime == null
                                     || (m.StartTime < meeting.EndTime && meeting.StartTime < m.EndTime))
                            .FirstOrDefaultAsync(ct);

                        if (conflictingMeeting != null)
                            return ApiResponse<GetMeetingResponse>.ConflictResponse(
                                "One or more guests already have a meeting scheduled at this date/time.");
                    }

                    meeting.Guests.Clear();
                    foreach (var g in guests) meeting.Guests.Add(g);
                }

                meeting.SetUpdateAudit(_currentUser.UserId);
                await _unitOfWork.SaveChangesAsync(ct);

                var response = new GetMeetingResponse
                {
                    Id = meeting.Id,
                    Name = meeting.Name,
                    Date = meeting.Date,
                    Location = meeting.Location,
                    StartTime = meeting.StartTime,
                    EndTime = meeting.EndTime,
                    MeetingAgenda = meeting.MeetingAgenda,
                    EventId = meeting.EventId,
                    Guests = meeting.Guests
                        .Select(g => new GuestInfo { Id = g.Id, Name = $"{g.FirstName} {g.LastName}".Trim() })
                        .ToList(),
                };

                return ApiResponse<GetMeetingResponse>.SuccessResponse(response, "Meeting updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating meeting {MeetingId}", request.MeetId);
                return ApiResponse<GetMeetingResponse>.ServerErrorResponse("Error updating meeting.");
            }
        }
    }
}
