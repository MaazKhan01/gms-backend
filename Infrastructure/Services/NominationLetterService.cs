using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.NominationLetter;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 4 — the official letter naming the delegation, and its back-and-forth
/// with the host.
///
/// One letter per mission. The row carries the CURRENT state; every issued
/// revision is kept as a version with the roster it was generated from, and every
/// state change is appended to the log. Re-issuing after the host asks for
/// changes produces a new version rather than overwriting — the host may still be
/// holding the old one.
///
/// Rendering the document is out of scope here: generation pins the roster and
/// bumps the version, and <c>DocumentUrl</c> is set by whatever produces the PDF.
/// </summary>
public class NominationLetterService(
    IUnitOfWork _unitOfWork,
    ILogger<NominationLetterService> _logger) : INominationLetterService
{
    private static readonly string[] Languages = { "en", "ar" };

    public async Task<ApiResponse<NominationLetterResponse>> GetAsync(Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<NominationLetterResponse>.NotFoundResponse("Mission not found.");

        var letter = await LoadLetterAsync(mission.Id, tracking: false, ct);

        // No row yet: hand back a shell so the screen renders before first use
        // rather than 404-ing a mission that simply has no letter.
        if (letter == null)
        {
            return ApiResponse<NominationLetterResponse>.SuccessResponse(new NominationLetterResponse
            {
                EventId = mission.PublicId,
                MissionTitle = mission.Title,
                Status = NominationLetterStatuses.NotGenerated,
                HostName = mission.HostName,
                HostEmail = mission.HostEmail,
                LiveRosterCount = await RosterCountAsync(mission.Id, ct),
            });
        }

        return ApiResponse<NominationLetterResponse>.SuccessResponse(await MapAsync(letter, mission, ct));
    }

    public async Task<ApiResponse<NominationLetterVersionDetailResponse>> GetVersionAsync(
        Guid versionId, CancellationToken ct = default)
    {
        if (versionId == Guid.Empty)
            return ApiResponse<NominationLetterVersionDetailResponse>.ErrorResponse("Version id is required.");

        var version = await _unitOfWork.NominationLetterVersions.QueryNoTracking()
            .FirstOrDefaultAsync(v => v.PublicId == versionId, ct);
        if (version == null)
            return ApiResponse<NominationLetterVersionDetailResponse>.NotFoundResponse("Version not found.");

        var roster = ParseRoster(version.RosterSnapshotJson);

        return ApiResponse<NominationLetterVersionDetailResponse>.SuccessResponse(
            new NominationLetterVersionDetailResponse
            {
                Id = version.PublicId,
                Version = version.Version,
                GeneratedOn = version.GeneratedOn,
                Language = version.Language,
                DocumentUrl = version.DocumentUrl,
                RosterCount = roster.Count,
                Roster = roster,
            });
    }

    // ── Generate ─────────────────────────────────────────────────────────

    public async Task<ApiResponse<NominationLetterResponse>> GenerateAsync(
        GenerateLetterRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.EventId == Guid.Empty)
            return ApiResponse<NominationLetterResponse>.ErrorResponse("Mission id is required.");

        var language = NormalizeLanguage(request.Language);
        if (language == null)
            return ApiResponse<NominationLetterResponse>.ErrorResponse("Language must be 'en' or 'ar'.");

        var mission = await FindMissionAsync(request.EventId, ct);
        if (mission == null)
            return ApiResponse<NominationLetterResponse>.NotFoundResponse("Mission not found.");

        var roster = await LoadRosterSnapshotAsync(mission.Id, ct);
        if (roster.Count == 0)
            return ApiResponse<NominationLetterResponse>.ErrorResponse(
                "Nominate at least one delegate before generating the letter.");

        // A rejected nomination must never reach the host. Pending is allowed —
        // HR often signs off while the letter is being drafted — so only a
        // rejection blocks.
        var rejected = await _unitOfWork.EventGuests.QueryNoTracking()
            .CountAsync(eg => eg.EventId == mission.Id
                           && eg.HrVerificationStatus == HrVerificationStatuses.Rejected, ct);
        if (rejected > 0)
            return ApiResponse<NominationLetterResponse>.ConflictResponse(
                $"{rejected} nomination(s) were rejected by HR. Remove or re-verify them first.",
                "ROSTER_HAS_REJECTED");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var letter = await LoadLetterAsync(mission.Id, tracking: true, ct);
            if (letter == null)
            {
                letter = new NominationLetter
                {
                    EventId = mission.Id,
                    Status = NominationLetterStatuses.NotGenerated,
                    CurrentVersion = 0,
                };
                letter.SetCreationAudit(userId);
                await _unitOfWork.NominationLetters.AddAsync(letter, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var next = letter.CurrentVersion + 1;
            var now = DateTime.UtcNow;

            await _unitOfWork.NominationLetterVersions.AddAsync(new NominationLetterVersion
            {
                NominationLetterId = letter.Id,
                Version = next,
                GeneratedOn = now,
                Language = language,
                DocumentUrl = string.IsNullOrWhiteSpace(request.DocumentUrl) ? null : request.DocumentUrl.Trim(),
                RosterSnapshotJson = JsonSerializer.Serialize(roster),
                CreatedBy = userId == 0 ? null : userId,
                CreatedAt = now,
            }, ct);

            // A new version is unsent by definition, so the track restarts at
            // draft even if the previous one was acknowledged — what the host
            // agreed to is no longer what the letter says.
            letter.CurrentVersion = next;
            letter.Status = NominationLetterStatuses.Draft;
            letter.Language = language;
            letter.GeneratedOn = now;
            letter.SentOn = null;
            letter.RespondedOn = null;
            letter.MessageId = null;
            letter.SetUpdateAudit(userId);
            _unitOfWork.NominationLetters.Update(letter);

            await AddHistoryAsync(letter.Id, $"Letter generated (v{next})",
                $"{roster.Count} delegate(s), {language}", userId, now, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync();

            return await GetAsync(mission.PublicId, ct);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Generating the nomination letter for mission {EventId} failed", request.EventId);
            return ApiResponse<NominationLetterResponse>.ServerErrorResponse("The letter could not be generated.");
        }
    }

    // ── Send / respond ───────────────────────────────────────────────────

    public async Task<ApiResponse<NominationLetterResponse>> SendAsync(
        SendLetterRequest request, int userId, CancellationToken ct = default)
    {
        var (letter, mission, error) = await LoadForTransitionAsync(request?.EventId ?? Guid.Empty, ct);
        if (error != null) return error;

        if (letter.CurrentVersion == 0)
            return ApiResponse<NominationLetterResponse>.ConflictResponse(
                "Generate the letter before sending it.", "LETTER_NOT_GENERATED");

        var hostEmail = string.IsNullOrWhiteSpace(request.HostEmail) ? mission.HostEmail : request.HostEmail.Trim();
        if (string.IsNullOrWhiteSpace(hostEmail))
            return ApiResponse<NominationLetterResponse>.ErrorResponse(
                "No host email on the mission. Add one, or supply it with the send.");
        if (!hostEmail.Contains('@'))
            return ApiResponse<NominationLetterResponse>.ErrorResponse("Host email is not a valid address.");

        // Re-sending is allowed from sent and changes_requested — that is exactly
        // the "resend after they asked for changes" step. Only acknowledged is
        // closed: re-issue a version first if the roster moved on.
        if (letter.Status == NominationLetterStatuses.Acknowledged)
            return ApiResponse<NominationLetterResponse>.ConflictResponse(
                "The host has already acknowledged this version. Generate a new one to send again.",
                "LETTER_ALREADY_ACKNOWLEDGED");

        var now = DateTime.UtcNow;
        var resend = letter.SentOn.HasValue;

        letter.Status = NominationLetterStatuses.Sent;
        letter.SentOn = now;
        letter.RespondedOn = null;
        letter.MessageId = string.IsNullOrWhiteSpace(request.MessageId) ? null : request.MessageId.Trim();
        letter.SetUpdateAudit(userId);
        _unitOfWork.NominationLetters.Update(letter);

        var detail = $"to {hostEmail}"
                   + (letter.MessageId != null ? $" · message {letter.MessageId}" : string.Empty)
                   + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" · {request.Note.Trim()}");
        await AddHistoryAsync(letter.Id,
            $"{(resend ? "Re-sent" : "Sent")} to host (v{letter.CurrentVersion})", detail, userId, now, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(mission.PublicId, ct);
    }

    public Task<ApiResponse<NominationLetterResponse>> AcknowledgeAsync(
        LetterResponseRequest request, int userId, CancellationToken ct = default)
        => RespondAsync(request, NominationLetterStatuses.Acknowledged, userId, ct);

    public Task<ApiResponse<NominationLetterResponse>> RequestChangesAsync(
        LetterResponseRequest request, int userId, CancellationToken ct = default)
    {
        // Without the host's note nobody knows what to change.
        if (string.IsNullOrWhiteSpace(request?.Note))
            return Task.FromResult(ApiResponse<NominationLetterResponse>.ErrorResponse(
                "A note is required — record what the host asked to change."));

        return RespondAsync(request, NominationLetterStatuses.ChangesRequested, userId, ct);
    }

    /// <summary>
    /// Records the host's reply. Both outcomes are logged manually by Protocol —
    /// the host answers by email, outside this system, so there is nothing to
    /// detect automatically.
    /// </summary>
    private async Task<ApiResponse<NominationLetterResponse>> RespondAsync(
        LetterResponseRequest request, string status, int userId, CancellationToken ct)
    {
        var (letter, mission, error) = await LoadForTransitionAsync(request?.EventId ?? Guid.Empty, ct);
        if (error != null) return error;

        // A reply only makes sense once something was sent.
        if (letter.Status != NominationLetterStatuses.Sent &&
            letter.Status != NominationLetterStatuses.ChangesRequested)
            return ApiResponse<NominationLetterResponse>.ConflictResponse(
                "The letter has not been sent to the host yet.", "LETTER_NOT_SENT");

        var now = DateTime.UtcNow;
        letter.Status = status;
        letter.RespondedOn = now;
        if (!string.IsNullOrWhiteSpace(request.Note)) letter.HostNotes = request.Note.Trim();
        letter.SetUpdateAudit(userId);
        _unitOfWork.NominationLetters.Update(letter);

        var action = status == NominationLetterStatuses.Acknowledged
            ? $"Host acknowledged (v{letter.CurrentVersion})"
            : $"Host requested changes (v{letter.CurrentVersion})";
        await AddHistoryAsync(letter.Id, action, request.Note?.Trim(), userId, now, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(mission.PublicId, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Task<Event> FindMissionAsync(Guid eventId, CancellationToken ct)
        => eventId == Guid.Empty
            ? Task.FromResult<Event>(null)
            : _unitOfWork.Events.QueryNoTracking().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

    private Task<NominationLetter> LoadLetterAsync(int missionId, bool tracking, CancellationToken ct)
    {
        var q = tracking ? _unitOfWork.NominationLetters.Query() : _unitOfWork.NominationLetters.QueryNoTracking();
        return q.FirstOrDefaultAsync(l => l.EventId == missionId, ct);
    }

    private async Task<(NominationLetter Letter, Event Mission, ApiResponse<NominationLetterResponse> Error)>
        LoadForTransitionAsync(Guid eventId, CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            return (null, null, ApiResponse<NominationLetterResponse>.ErrorResponse("Mission id is required."));

        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return (null, null, ApiResponse<NominationLetterResponse>.NotFoundResponse("Mission not found."));

        var letter = await LoadLetterAsync(mission.Id, tracking: true, ct);
        if (letter == null)
            return (null, null, ApiResponse<NominationLetterResponse>.ConflictResponse(
                "Generate the letter before sending it.", "LETTER_NOT_GENERATED"));

        return (letter, mission, null);
    }

    private Task<int> RosterCountAsync(int missionId, CancellationToken ct)
        => _unitOfWork.EventGuests.QueryNoTracking().CountAsync(eg => eg.EventId == missionId, ct);

    private Task<List<NominationLetterRosterEntry>> LoadRosterSnapshotAsync(int missionId, CancellationToken ct)
        => _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.EventId == missionId)
            .OrderBy(eg => eg.Subgroup).ThenBy(eg => eg.Guest.FirstName)
            .Select(eg => new NominationLetterRosterEntry
            {
                ParticipationId = eg.PublicId,
                FullName = (eg.Guest.FirstName + " " + eg.Guest.LastName).Trim(),
                JobTitle = eg.Guest.JobTitle,
                MissionRole = eg.MissionRole != null ? eg.MissionRole.Name : null,
                Subgroup = eg.Subgroup,
                Department = eg.Guest.Department != null ? eg.Guest.Department.Name : null,
                Nationality = eg.Guest.Nationality != null ? eg.Guest.Nationality.Name : null,
                PassportNumber = eg.Guest.PassportNumber,
                PassportExpiry = eg.Guest.PassportExpiry,
            })
            .ToListAsync(ct);

    private static List<NominationLetterRosterEntry> ParseRoster(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<NominationLetterRosterEntry>>(json) ?? new(); }
        // A snapshot that will not parse is bad data, not a reason to 500 the
        // whole screen — the version still lists, just without its roster.
        catch (JsonException) { return new(); }
    }

    private async Task AddHistoryAsync(
        int letterId, string action, string note, int userId, DateTime occurredOn, CancellationToken ct)
        => await _unitOfWork.NominationLetterHistory.AddAsync(new NominationLetterHistory
        {
            NominationLetterId = letterId,
            OccurredOn = occurredOn,
            Action = action,
            Note = string.IsNullOrWhiteSpace(note) ? null : note,
            ActorId = userId == 0 ? null : userId,
            CreatedBy = userId == 0 ? null : userId,
            CreatedAt = occurredOn,
        }, ct);

    private static string NormalizeLanguage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "en";
        var v = value.Trim().ToLowerInvariant();
        return Languages.Contains(v) ? v : null;
    }

    private async Task<NominationLetterResponse> MapAsync(NominationLetter letter, Event mission, CancellationToken ct)
    {
        var versions = await _unitOfWork.NominationLetterVersions.QueryNoTracking()
            .Where(v => v.NominationLetterId == letter.Id)
            .OrderByDescending(v => v.Version)
            .Select(v => new { v.PublicId, v.Version, v.GeneratedOn, v.Language, v.DocumentUrl, v.RosterSnapshotJson })
            .ToListAsync(ct);

        var history = await _unitOfWork.NominationLetterHistory.QueryNoTracking()
            .Where(h => h.NominationLetterId == letter.Id)
            .OrderByDescending(h => h.OccurredOn)
            .Select(h => new NominationLetterHistoryResponse
            {
                OccurredOn = h.OccurredOn,
                Action = h.Action,
                Note = h.Note,
                ActorName = h.Actor != null ? (h.Actor.FirstName + " " + h.Actor.LastName).Trim() : null,
            })
            .ToListAsync(ct);

        var current = versions.FirstOrDefault(v => v.Version == letter.CurrentVersion);
        var versionCount = current == null ? 0 : ParseRoster(current.RosterSnapshotJson).Count;
        var liveCount = await RosterCountAsync(mission.Id, ct);

        return new NominationLetterResponse
        {
            Id = letter.PublicId,
            EventId = mission.PublicId,
            MissionTitle = mission.Title,
            Status = letter.Status,
            CurrentVersion = letter.CurrentVersion,
            Language = letter.Language,
            GeneratedOn = letter.GeneratedOn,
            SentOn = letter.SentOn,
            RespondedOn = letter.RespondedOn,
            MessageId = letter.MessageId,
            HostNotes = letter.HostNotes,
            HostName = mission.HostName,
            HostEmail = mission.HostEmail,
            VersionRosterCount = versionCount,
            LiveRosterCount = liveCount,
            RosterChangedSinceGenerated = letter.CurrentVersion > 0 && versionCount != liveCount,
            Versions = versions.Select(v => new NominationLetterVersionResponse
            {
                Id = v.PublicId,
                Version = v.Version,
                GeneratedOn = v.GeneratedOn,
                Language = v.Language,
                DocumentUrl = v.DocumentUrl,
                RosterCount = ParseRoster(v.RosterSnapshotJson).Count,
            }).ToList(),
            History = history,
        };
    }
}
