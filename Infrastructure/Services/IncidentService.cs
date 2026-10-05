using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Incident;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 7 — help requests raised on the ground: a delegate is late, lost, unwell,
/// or something else went wrong.
///
/// An incident may concern one delegate or the delegation as a whole, which is why
/// the participation link is optional. RaisedVia records whether the delegate
/// reported it themselves (app) or an officer logged it for them (portal) — the
/// distinction matters when the mission is reviewed afterwards.
/// </summary>
public class IncidentService(IUnitOfWork _unitOfWork) : IIncidentService
{
    private const int MaxDescriptionLength = 2000;

    // ── Reads ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<IncidentResponse>>> GetAsync(
        Guid eventId, string status, string severity, string category, Guid? delegateId,
        CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<IncidentResponse>>.NotFoundResponse("Mission not found.");

        var query = _unitOfWork.Incidents.QueryNoTracking().Where(i => i.EventId == mission.Id);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var value = status.Trim().ToLowerInvariant();
            if (!IncidentStatuses.IsValid(value))
                return ApiResponse<List<IncidentResponse>>.ErrorResponse(
                    $"Status must be one of: {string.Join(", ", IncidentStatuses.All)}.");
            query = query.Where(i => i.Status == value);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var value = severity.Trim().ToLowerInvariant();
            if (!IncidentSeverities.IsValid(value))
                return ApiResponse<List<IncidentResponse>>.ErrorResponse(
                    $"Severity must be one of: {string.Join(", ", IncidentSeverities.All)}.");
            query = query.Where(i => i.Severity == value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var value = category.Trim().ToLowerInvariant();
            if (!IncidentCategories.IsValid(value))
                return ApiResponse<List<IncidentResponse>>.ErrorResponse(
                    $"Category must be one of: {string.Join(", ", IncidentCategories.All)}.");
            query = query.Where(i => i.Category == value);
        }

        if (delegateId.HasValue && delegateId.Value != Guid.Empty)
        {
            var participation = await FindParticipationAsync(delegateId.Value, mission.Id, ct);
            if (participation == null)
                return ApiResponse<List<IncidentResponse>>.NotFoundResponse("Delegate not found on this mission.");
            query = query.Where(i => i.EventGuestId == participation.Id);
        }

        // Open work first — an ops screen is a to-do list, not an archive.
        var items = await Project(query)
            .OrderBy(i => i.Status == IncidentStatuses.Resolved)
            .ThenByDescending(i => i.RaisedAt)
            .ToListAsync(ct);

        return ApiResponse<List<IncidentResponse>>.SuccessResponse(items);
    }

    public async Task<ApiResponse<IncidentResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<IncidentResponse>.ErrorResponse("Incident id is required.");

        var item = await Project(_unitOfWork.Incidents.QueryNoTracking().Where(i => i.PublicId == id))
            .FirstOrDefaultAsync(ct);

        return item == null
            ? ApiResponse<IncidentResponse>.NotFoundResponse("Incident not found.")
            : ApiResponse<IncidentResponse>.SuccessResponse(item);
    }

    public async Task<ApiResponse<IncidentSummaryResponse>> GetSummaryAsync(
        Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<IncidentSummaryResponse>.NotFoundResponse("Mission not found.");

        var rows = await _unitOfWork.Incidents.QueryNoTracking()
            .Where(i => i.EventId == mission.Id)
            .Select(i => new { i.Status, i.Severity, i.Category })
            .ToListAsync(ct);

        var summary = new IncidentSummaryResponse
        {
            Total = rows.Count,
            Open = rows.Count(r => !IncidentStatuses.IsClosed(r.Status)),
            Resolved = rows.Count(r => IncidentStatuses.IsClosed(r.Status)),
        };

        // Every key present, including the zeroes, so the client renders a stable
        // set of tiles rather than whichever buckets happen to be non-empty.
        foreach (var s in IncidentStatuses.All) summary.ByStatus[s] = rows.Count(r => r.Status == s);
        foreach (var s in IncidentSeverities.All) summary.BySeverity[s] = rows.Count(r => r.Severity == s);
        foreach (var c in IncidentCategories.All) summary.ByCategory[c] = rows.Count(r => r.Category == c);

        return ApiResponse<IncidentSummaryResponse>.SuccessResponse(summary);
    }

    // ── Writes ───────────────────────────────────────────────────────────

    public async Task<ApiResponse<IncidentResponse>> CreateAsync(
        CreateIncidentRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.EventId == Guid.Empty)
            return ApiResponse<IncidentResponse>.ErrorResponse("Mission id is required.");

        var mission = await FindMissionAsync(request.EventId, ct);
        if (mission == null)
            return ApiResponse<IncidentResponse>.NotFoundResponse("Mission not found.");

        var category = request.Category?.Trim().ToLowerInvariant();
        if (!IncidentCategories.IsValid(category))
            return ApiResponse<IncidentResponse>.ErrorResponse(
                $"Category must be one of: {string.Join(", ", IncidentCategories.All)}.");

        var severity = request.Severity?.Trim().ToLowerInvariant();
        if (!IncidentSeverities.IsValid(severity))
            return ApiResponse<IncidentResponse>.ErrorResponse(
                $"Severity must be one of: {string.Join(", ", IncidentSeverities.All)}.");

        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            return ApiResponse<IncidentResponse>.ErrorResponse("Description is required.");
        if (description.Length > MaxDescriptionLength)
            return ApiResponse<IncidentResponse>.ErrorResponse(
                $"Description cannot exceed {MaxDescriptionLength} characters.");

        int? participationId = null;
        if (request.DelegateId.HasValue && request.DelegateId.Value != Guid.Empty)
        {
            var participation = await FindParticipationAsync(request.DelegateId.Value, mission.Id, ct);
            if (participation == null)
                return ApiResponse<IncidentResponse>.NotFoundResponse("Delegate not found on this mission.");
            participationId = participation.Id;
        }

        var incident = new Incident
        {
            EventId = mission.Id,
            EventGuestId = participationId,
            Category = category,
            Severity = severity,
            Status = IncidentStatuses.Open,
            Description = description,
            RaisedBy = userId == 0 ? null : userId,
            // This endpoint is the officer's. The delegate-facing app path sets
            // IncidentChannels.App when it lands.
            RaisedVia = IncidentChannels.Portal,
        };
        incident.SetCreationAudit(userId);

        await _unitOfWork.Incidents.AddAsync(incident, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadAsync(incident.PublicId, "Incident logged.", ct);
    }

    public async Task<ApiResponse<IncidentResponse>> UpdateAsync(
        UpdateIncidentRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.Id == Guid.Empty)
            return ApiResponse<IncidentResponse>.ErrorResponse("Incident id is required.");

        var incident = await _unitOfWork.Incidents.Query()
            .FirstOrDefaultAsync(i => i.PublicId == request.Id, ct);
        if (incident == null)
            return ApiResponse<IncidentResponse>.NotFoundResponse("Incident not found.");

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim().ToLowerInvariant();
            if (!IncidentCategories.IsValid(category))
                return ApiResponse<IncidentResponse>.ErrorResponse(
                    $"Category must be one of: {string.Join(", ", IncidentCategories.All)}.");
            incident.Category = category;
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToLowerInvariant();
            if (!IncidentSeverities.IsValid(severity))
                return ApiResponse<IncidentResponse>.ErrorResponse(
                    $"Severity must be one of: {string.Join(", ", IncidentSeverities.All)}.");
            incident.Severity = severity;
        }

        if (request.Description != null)
        {
            var description = request.Description.Trim();
            if (string.IsNullOrWhiteSpace(description))
                return ApiResponse<IncidentResponse>.ErrorResponse("Description cannot be cleared.");
            if (description.Length > MaxDescriptionLength)
                return ApiResponse<IncidentResponse>.ErrorResponse(
                    $"Description cannot exceed {MaxDescriptionLength} characters.");
            incident.Description = description;
        }

        // Guid.Empty means "detach from the delegate"; null means "leave it alone".
        if (request.DelegateId.HasValue)
        {
            if (request.DelegateId.Value == Guid.Empty)
            {
                incident.EventGuestId = null;
            }
            else
            {
                var participation = await FindParticipationAsync(request.DelegateId.Value, incident.EventId, ct);
                if (participation == null)
                    return ApiResponse<IncidentResponse>.NotFoundResponse("Delegate not found on this mission.");
                incident.EventGuestId = participation.Id;
            }
        }

        incident.SetUpdateAudit(userId);
        _unitOfWork.Incidents.Update(incident);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadAsync(incident.PublicId, "Incident updated.", ct);
    }

    public async Task<ApiResponse<IncidentResponse>> ChangeStatusAsync(
        ChangeIncidentStatusRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.Id == Guid.Empty)
            return ApiResponse<IncidentResponse>.ErrorResponse("Incident id is required.");

        var status = request.Status?.Trim().ToLowerInvariant();
        if (!IncidentStatuses.IsValid(status))
            return ApiResponse<IncidentResponse>.ErrorResponse(
                $"Status must be one of: {string.Join(", ", IncidentStatuses.All)}.");

        var incident = await _unitOfWork.Incidents.Query()
            .FirstOrDefaultAsync(i => i.PublicId == request.Id, ct);
        if (incident == null)
            return ApiResponse<IncidentResponse>.NotFoundResponse("Incident not found.");

        if (incident.Status == status)
            return ApiResponse<IncidentResponse>.ConflictResponse(
                $"This incident is already {status}.", "INCIDENT_STATUS_UNCHANGED");

        var note = request.Note?.Trim();

        // Closing something has to say how it ended; the other moves are
        // self-explanatory from the status alone.
        if (IncidentStatuses.IsClosed(status) && string.IsNullOrWhiteSpace(note))
            return ApiResponse<IncidentResponse>.ErrorResponse("A note is required when resolving an incident.");

        if (!string.IsNullOrWhiteSpace(note))
        {
            var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm} → {status}] {note}";
            var combined = string.IsNullOrWhiteSpace(incident.Description)
                ? line
                : incident.Description + "\n" + line;

            // The column is capped; drop the oldest text rather than the note that
            // explains the move.
            incident.Description = combined.Length <= MaxDescriptionLength
                ? combined
                : combined[^MaxDescriptionLength..];
        }

        incident.Status = status;

        if (IncidentStatuses.IsClosed(status))
        {
            incident.ResolvedBy = userId == 0 ? null : userId;
            incident.ResolvedAt = DateTime.UtcNow;
        }
        else
        {
            // Reopened — the old resolution no longer describes it.
            incident.ResolvedBy = null;
            incident.ResolvedAt = null;
        }

        incident.SetUpdateAudit(userId);
        _unitOfWork.Incidents.Update(incident);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadAsync(incident.PublicId, $"Incident marked {status}.", ct);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<bool>.ErrorResponse("Incident id is required.");

        var incident = await _unitOfWork.Incidents.Query().FirstOrDefaultAsync(i => i.PublicId == id, ct);
        if (incident == null)
            return ApiResponse<bool>.NotFoundResponse("Incident not found.");

        incident.MarkAsDeleted(userId);
        _unitOfWork.Incidents.Update(incident);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Incident removed.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Task<Event> FindMissionAsync(Guid eventId, CancellationToken ct)
        => eventId == Guid.Empty
            ? Task.FromResult<Event>(null)
            : _unitOfWork.Events.QueryNoTracking().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

    /// <summary>Scoped to the mission on purpose: a participation id from another
    /// mission is a caller error, not a cross-mission link.</summary>
    private Task<EventGuest> FindParticipationAsync(Guid delegateId, int missionId, CancellationToken ct)
        => _unitOfWork.EventGuests.QueryNoTracking()
            .FirstOrDefaultAsync(eg => eg.PublicId == delegateId && eg.EventId == missionId, ct);

    private async Task<ApiResponse<IncidentResponse>> ReloadAsync(Guid publicId, string message, CancellationToken ct)
    {
        var item = await Project(_unitOfWork.Incidents.QueryNoTracking().Where(i => i.PublicId == publicId))
            .FirstOrDefaultAsync(ct);

        return ApiResponse<IncidentResponse>.SuccessResponse(item, message);
    }

    private static IQueryable<IncidentResponse> Project(IQueryable<Incident> query)
        => query.Select(i => new IncidentResponse
        {
            Id = i.PublicId,
            EventId = i.Event.PublicId,
            DelegateId = i.EventGuest != null ? i.EventGuest.PublicId : (Guid?)null,
            DelegateName = i.EventGuest != null
                ? (i.EventGuest.Guest.FirstName + " " + i.EventGuest.Guest.LastName).Trim()
                : null,
            Subgroup = i.EventGuest != null ? i.EventGuest.Subgroup : null,
            Category = i.Category,
            Severity = i.Severity,
            Status = i.Status,
            Description = i.Description,
            RaisedVia = i.RaisedVia,
            RaisedByName = i.RaisedByUser != null
                ? (i.RaisedByUser.FirstName + " " + i.RaisedByUser.LastName).Trim()
                : null,
            RaisedAt = i.CreatedAt,
            ResolvedByName = i.ResolvedByUser != null
                ? (i.ResolvedByUser.FirstName + " " + i.ResolvedByUser.LastName).Trim()
                : null,
            ResolvedAt = i.ResolvedAt,
            IsOpen = i.Status != IncidentStatuses.Resolved,
        });
}
