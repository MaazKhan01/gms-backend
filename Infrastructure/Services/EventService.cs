using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Event;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class EventService(IUnitOfWork _unitOfWork, IMapper _mapper) : IEventService
{
    private static readonly string[] ValidStatuses = { "planning", "active", "completed", "cancelled" };

    // Allowed lifecycle transitions (see R&D §4.2).
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["planning"]  = new[] { "active", "cancelled" },
        ["active"]    = new[] { "completed", "cancelled" },
        ["completed"] = Array.Empty<string>(),
        ["cancelled"] = new[] { "planning" },
    };

    public async Task<ApiResponse<PaginatedResponse<EventResponse>>> GetEventsAsync(PagedRequest request, string status, CancellationToken ct = default)
    {
        IQueryable<Event> query = _unitOfWork.Events.QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(e => e.Title.Contains(term) || e.VenueName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.StartDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(e => e.Sessions)
            .ToListAsync(ct);

        var mapped = _mapper.Map<List<EventResponse>>(items);
        var paged = new PaginatedResponse<EventResponse>(mapped, total, request.PageNumber, request.PageSize);
        return ApiResponse<PaginatedResponse<EventResponse>>.SuccessResponse(paged);
    }

    public async Task<ApiResponse<EventResponse>> GetEventByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.Query()
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (ev == null)
            return ApiResponse<EventResponse>.NotFoundResponse("Event not found");

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev));
    }

    public async Task<ApiResponse<EventResponse>> CreateEventAsync(CreateEventRequest request, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResponse<EventResponse>.ErrorResponse("Title is required");

        var status = string.IsNullOrWhiteSpace(request.Status) ? "planning" : request.Status.ToLowerInvariant();
        if (!ValidStatuses.Contains(status))
            return ApiResponse<EventResponse>.ErrorResponse($"Invalid status '{request.Status}'");

        var ev = _mapper.Map<Event>(request);
        ev.Id = Guid.NewGuid();
        ev.Status = status;
        ev.AppKey = await UniqueAppKeyAsync(Slugify(request.Title), ct);
        ev.SetCreationAudit(userId);

        await _unitOfWork.Events.AddAsync(ev, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev), "Event created");
    }

    public async Task<ApiResponse<EventResponse>> UpdateEventAsync(Guid id, UpdateEventRequest request, Guid userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByIdAsync(id, ct);
        if (ev == null)
            return ApiResponse<EventResponse>.NotFoundResponse("Event not found");

        ev.Title = request.Title ?? ev.Title;
        ev.Type = request.Type ?? ev.Type;
        ev.Theme = request.Theme ?? ev.Theme;
        ev.VenueName = request.VenueName ?? ev.VenueName;
        ev.StartDate = request.StartDate ?? ev.StartDate;
        ev.EndDate = request.EndDate ?? ev.EndDate;
        ev.ImageUrl = request.ImageUrl ?? ev.ImageUrl;
        ev.ThemeAccent = request.ThemeAccent ?? ev.ThemeAccent;
        ev.ThemeSecondary = request.ThemeSecondary ?? ev.ThemeSecondary;
        ev.LogoDarkUrl = request.LogoDarkUrl ?? ev.LogoDarkUrl;
        ev.LogoLightUrl = request.LogoLightUrl ?? ev.LogoLightUrl;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.ToLowerInvariant();
            if (!ValidStatuses.Contains(status))
                return ApiResponse<EventResponse>.ErrorResponse($"Invalid status '{request.Status}'");
            ev.Status = status;
        }

        ev.SetUpdateAudit(userId);
        _unitOfWork.Events.Update(ev);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev), "Event updated");
    }

    public async Task<ApiResponse<EventResponse>> UpdateStatusAsync(Guid id, string status, Guid userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByIdAsync(id, ct);
        if (ev == null)
            return ApiResponse<EventResponse>.NotFoundResponse("Event not found");

        var target = (status ?? string.Empty).ToLowerInvariant();
        if (!ValidStatuses.Contains(target))
            return ApiResponse<EventResponse>.ErrorResponse($"Invalid status '{status}'");

        var current = (ev.Status ?? "planning").ToLowerInvariant();
        if (current != target && (!Transitions.TryGetValue(current, out var allowed) || !allowed.Contains(target)))
            return ApiResponse<EventResponse>.ErrorResponse($"Cannot transition from '{current}' to '{target}'");

        ev.Status = target;
        ev.SetUpdateAudit(userId);
        _unitOfWork.Events.Update(ev);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev), "Status updated");
    }

    public async Task<ApiResponse<bool>> DeleteEventAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByIdAsync(id, ct);
        if (ev == null)
            return ApiResponse<bool>.NotFoundResponse("Event not found");

        ev.MarkAsDeleted(userId);
        _unitOfWork.Events.Update(ev);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Event deleted");
    }

    // ── Sessions ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<SessionResponse>>> GetSessionsAsync(Guid eventId, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.Sessions.Query()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Date).ThenBy(s => s.Time)
            .ToListAsync(ct);

        return ApiResponse<List<SessionResponse>>.SuccessResponse(_mapper.Map<List<SessionResponse>>(sessions));
    }

    public async Task<ApiResponse<SessionResponse>> AddSessionAsync(Guid eventId, CreateSessionRequest request, Guid userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByIdAsync(eventId, ct);
        if (ev == null)
            return ApiResponse<SessionResponse>.NotFoundResponse("Event not found");
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResponse<SessionResponse>.ErrorResponse("Session title is required");

        var dateError = ValidateSessionDate(ev, request.Date);
        if (dateError != null)
            return ApiResponse<SessionResponse>.ErrorResponse(dateError);

        var session = _mapper.Map<Session>(request);
        session.Id = Guid.NewGuid();
        session.EventId = eventId;
        session.SetCreationAudit(userId);

        await _unitOfWork.Sessions.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<SessionResponse>.SuccessResponse(_mapper.Map<SessionResponse>(session), "Session added");
    }

    public async Task<ApiResponse<SessionResponse>> UpdateSessionAsync(Guid eventId, Guid sessionId, UpdateSessionRequest request, Guid userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.Sessions.FindFirstOrDefaultAsync(s => s.Id == sessionId && s.EventId == eventId, ct);
        if (session == null)
            return ApiResponse<SessionResponse>.NotFoundResponse("Session not found");

        var ev = await _unitOfWork.Events.GetByIdAsync(eventId, ct);
        var dateError = ValidateSessionDate(ev, request.Date ?? session.Date);
        if (dateError != null)
            return ApiResponse<SessionResponse>.ErrorResponse(dateError);

        session.Title = request.Title ?? session.Title;
        session.Date = request.Date ?? session.Date;
        session.Time = request.Time ?? session.Time;
        session.VenueName = request.VenueName ?? session.VenueName;
        session.Room = request.Room ?? session.Room;
        session.Speaker = request.Speaker ?? session.Speaker;
        if (request.Capacity > 0) session.Capacity = request.Capacity;

        session.SetUpdateAudit(userId);
        _unitOfWork.Sessions.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<SessionResponse>.SuccessResponse(_mapper.Map<SessionResponse>(session), "Session updated");
    }

    public async Task<ApiResponse<bool>> DeleteSessionAsync(Guid eventId, Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.Sessions.FindFirstOrDefaultAsync(s => s.Id == sessionId && s.EventId == eventId, ct);
        if (session == null)
            return ApiResponse<bool>.NotFoundResponse("Session not found");

        session.MarkAsDeleted(userId);
        _unitOfWork.Sessions.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Session deleted");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // A session must fall within its event's date range (when both are set).
    private static string ValidateSessionDate(Event ev, DateOnly? date)
    {
        if (ev == null || date is null) return null;
        if (ev.StartDate is { } start && date < start)
            return "Session date can't be before the event start date";
        if (ev.EndDate is { } end && date > end)
            return "Session date can't be after the event end date";
        return null;
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "event";
        var slug = Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "event" : slug;
    }

    private async Task<string> UniqueAppKeyAsync(string baseKey, CancellationToken ct)
    {
        var key = baseKey;
        var i = 2;
        while (await _unitOfWork.Events.AnyAsync(e => e.AppKey == key, ct))
            key = $"{baseKey}-{i++}";
        return key;
    }
}
