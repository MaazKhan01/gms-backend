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
    }
}
