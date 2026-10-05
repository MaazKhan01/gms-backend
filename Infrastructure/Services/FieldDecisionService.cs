using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.FieldDecision;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 7 — the Head of Delegation's decision log.
///
/// Deliberately thin: no approval, no status, no reversal. The decision was taken
/// in the field before anyone typed it in, so this records it rather than
/// governing it. A decision that is later changed is a second entry, not an edit
/// of the first — editing exists only to correct what was typed.
/// </summary>
public class FieldDecisionService(IUnitOfWork _unitOfWork) : IFieldDecisionService
{
    private const int MaxNoteLength = 2000;

    /// <summary>Tolerance for a client clock running ahead of the server's.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public async Task<ApiResponse<List<FieldDecisionResponse>>> GetAsync(
        Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<FieldDecisionResponse>>.NotFoundResponse("Mission not found.");

        var items = await Project(
                _unitOfWork.FieldDecisions.QueryNoTracking().Where(d => d.EventId == mission.Id))
            .OrderByDescending(d => d.DecidedAt)
            .ToListAsync(ct);

        return ApiResponse<List<FieldDecisionResponse>>.SuccessResponse(items);
    }

    public async Task<ApiResponse<FieldDecisionResponse>> CreateAsync(
        CreateFieldDecisionRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.EventId == Guid.Empty)
            return ApiResponse<FieldDecisionResponse>.ErrorResponse("Mission id is required.");

        var mission = await FindMissionAsync(request.EventId, ct);
        if (mission == null)
            return ApiResponse<FieldDecisionResponse>.NotFoundResponse("Mission not found.");

        var note = request.DecisionNote?.Trim();
        var noteError = ValidateNote(note);
        if (noteError != null)
            return ApiResponse<FieldDecisionResponse>.ErrorResponse(noteError);

        var decidedAt = request.DecidedAt ?? DateTime.UtcNow;
        if (decidedAt > DateTime.UtcNow.Add(ClockSkew))
            return ApiResponse<FieldDecisionResponse>.ErrorResponse(
                "A decision cannot be logged with a future date.");

        var decision = new FieldDecision
        {
            EventId = mission.Id,
            DecisionNote = note,
            DecidedBy = userId == 0 ? null : userId,
            DecidedAt = decidedAt,
        };
        decision.SetCreationAudit(userId);

        await _unitOfWork.FieldDecisions.AddAsync(decision, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadAsync(decision.PublicId, "Decision logged.", ct);
    }

    public async Task<ApiResponse<FieldDecisionResponse>> UpdateAsync(
        UpdateFieldDecisionRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.Id == Guid.Empty)
            return ApiResponse<FieldDecisionResponse>.ErrorResponse("Decision id is required.");

        var decision = await _unitOfWork.FieldDecisions.Query()
            .FirstOrDefaultAsync(d => d.PublicId == request.Id, ct);
        if (decision == null)
            return ApiResponse<FieldDecisionResponse>.NotFoundResponse("Decision not found.");

        if (request.DecisionNote != null)
        {
            var note = request.DecisionNote.Trim();
            var noteError = ValidateNote(note);
            if (noteError != null)
                return ApiResponse<FieldDecisionResponse>.ErrorResponse(noteError);
            decision.DecisionNote = note;
        }

        if (request.DecidedAt.HasValue)
        {
            if (request.DecidedAt.Value > DateTime.UtcNow.Add(ClockSkew))
                return ApiResponse<FieldDecisionResponse>.ErrorResponse(
                    "A decision cannot be logged with a future date.");
            decision.DecidedAt = request.DecidedAt.Value;
        }

        decision.SetUpdateAudit(userId);
        _unitOfWork.FieldDecisions.Update(decision);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadAsync(decision.PublicId, "Decision updated.", ct);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<bool>.ErrorResponse("Decision id is required.");

        var decision = await _unitOfWork.FieldDecisions.Query()
            .FirstOrDefaultAsync(d => d.PublicId == id, ct);
        if (decision == null)
            return ApiResponse<bool>.NotFoundResponse("Decision not found.");

        decision.MarkAsDeleted(userId);
        _unitOfWork.FieldDecisions.Update(decision);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Decision removed.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string ValidateNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note)) return "Decision note is required.";
        if (note.Length > MaxNoteLength) return $"Decision note cannot exceed {MaxNoteLength} characters.";
        return null;
    }

    private Task<Event> FindMissionAsync(Guid eventId, CancellationToken ct)
        => eventId == Guid.Empty
            ? Task.FromResult<Event>(null)
            : _unitOfWork.Events.QueryNoTracking().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

    private async Task<ApiResponse<FieldDecisionResponse>> ReloadAsync(
        Guid publicId, string message, CancellationToken ct)
    {
        var item = await Project(
                _unitOfWork.FieldDecisions.QueryNoTracking().Where(d => d.PublicId == publicId))
            .FirstOrDefaultAsync(ct);

        return ApiResponse<FieldDecisionResponse>.SuccessResponse(item, message);
    }

    private static IQueryable<FieldDecisionResponse> Project(IQueryable<FieldDecision> query)
        => query.Select(d => new FieldDecisionResponse
        {
            Id = d.PublicId,
            EventId = d.Event.PublicId,
            DecisionNote = d.DecisionNote,
            DecidedByName = d.DecidedByUser != null
                ? (d.DecidedByUser.FirstName + " " + d.DecidedByUser.LastName).Trim()
                : null,
            DecidedAt = d.DecidedAt,
        });
}
