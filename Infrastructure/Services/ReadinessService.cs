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
using Core.ViewModel.Readiness;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 6 — the pre-departure gate. Five checks per delegate: passport, visa,
/// flight, accommodation, transport.
///
/// Every check is DERIVED from the real booking data, so nothing here can drift
/// from what the services layer actually holds. The only stored state is a
/// waiver: a coordinator overriding one red item with a reason, which is the sole
/// record of why someone travelled without it.
/// </summary>
public class ReadinessService(IUnitOfWork _unitOfWork) : IReadinessService
{
    private const string Met = "met";
    private const string Waived = "waived";
    private const string Missing = "missing";

    public async Task<ApiResponse<List<ReadinessResponse>>> GetAsync(
        Guid eventId, bool onlyNotReady, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<ReadinessResponse>>.NotFoundResponse("Mission not found.");

        var rows = await BuildAsync(mission, ct);
        if (onlyNotReady) rows = rows.Where(r => !r.TravelReady).ToList();

        return ApiResponse<List<ReadinessResponse>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<ReadinessSummaryResponse>> GetSummaryAsync(
        Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<ReadinessSummaryResponse>.NotFoundResponse("Mission not found.");

        var rows = await BuildAsync(mission, ct);

        var summary = new ReadinessSummaryResponse
        {
            Total = rows.Count,
            TravelReady = rows.Count(r => r.TravelReady),
            NotReady = rows.Count(r => !r.TravelReady),
            ReadyWithWaivers = rows.Count(r => r.ReadyWithWaivers),
        };

        foreach (var key in ReadinessItems.All)
            summary.MissingByItem[key] = rows.Count(r => r.Items.Any(i => i.Key == key && i.Status == Missing));

        return ApiResponse<ReadinessSummaryResponse>.SuccessResponse(summary);
    }

    public async Task<ApiResponse<ReadinessResponse>> WaiveAsync(
        WaiveReadinessRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.Id == Guid.Empty)
            return ApiResponse<ReadinessResponse>.ErrorResponse("Delegate id is required.");

        var itemKey = request.ItemKey?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(itemKey) || !ReadinessItems.All.Contains(itemKey))
            return ApiResponse<ReadinessResponse>.ErrorResponse(
                $"Item must be one of: {string.Join(", ", ReadinessItems.All)}.");

        // Required in the service as well as the database — this log is the only
        // record of why the gate was skipped.
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<ReadinessResponse>.ErrorResponse("A reason is required to waive a readiness item.");

        var participation = await _unitOfWork.EventGuests.QueryNoTracking()
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.PublicId == request.Id, ct);
        if (participation == null)
            return ApiResponse<ReadinessResponse>.NotFoundResponse("Delegate not found on any mission.");

        if (await _unitOfWork.ReadinessWaivers.QueryNoTracking()
                .AnyAsync(w => w.EventGuestId == participation.Id && w.ItemKey == itemKey, ct))
            return ApiResponse<ReadinessResponse>.ConflictResponse(
                "That item is already waived for this delegate. Withdraw the existing waiver first.",
                "ITEM_ALREADY_WAIVED");

        // Waiving something that is already satisfied is almost certainly a
        // mistake, and it would leave a misleading "travelled without it" note.
        var current = await BuildOneAsync(participation, ct);
        if (current.Items.First(i => i.Key == itemKey).Status == Met)
            return ApiResponse<ReadinessResponse>.ConflictResponse(
                "That item is already met — no waiver is needed.", "ITEM_ALREADY_MET");

        var waiver = new ReadinessWaiver
        {
            EventGuestId = participation.Id,
            ItemKey = itemKey,
            Reason = request.Reason.Trim(),
            WaivedBy = userId == 0 ? null : userId,
            WaivedAt = DateTime.UtcNow,
        };
        waiver.SetCreationAudit(userId);

        await _unitOfWork.ReadinessWaivers.AddAsync(waiver, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<ReadinessResponse>.SuccessResponse(
            await BuildOneAsync(participation, ct), "Readiness item waived.");
    }

    public async Task<ApiResponse<ReadinessResponse>> RemoveWaiverAsync(
        Guid waiverId, int userId, CancellationToken ct = default)
    {
        if (waiverId == Guid.Empty)
            return ApiResponse<ReadinessResponse>.ErrorResponse("Waiver id is required.");

        var waiver = await _unitOfWork.ReadinessWaivers.Query()
            .FirstOrDefaultAsync(w => w.PublicId == waiverId, ct);
        if (waiver == null)
            return ApiResponse<ReadinessResponse>.NotFoundResponse("Waiver not found.");

        waiver.MarkAsDeleted(userId);
        _unitOfWork.ReadinessWaivers.Update(waiver);
        await _unitOfWork.SaveChangesAsync(ct);

        var participation = await _unitOfWork.EventGuests.QueryNoTracking()
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.Id == waiver.EventGuestId, ct);

        return ApiResponse<ReadinessResponse>.SuccessResponse(
            await BuildOneAsync(participation, ct), "Waiver withdrawn.");
    }

    // ── Building the checklist ───────────────────────────────────────────

    private Task<Event> FindMissionAsync(Guid eventId, CancellationToken ct)
        => eventId == Guid.Empty
            ? Task.FromResult<Event>(null)
            : _unitOfWork.Events.QueryNoTracking().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

    private async Task<ReadinessResponse> BuildOneAsync(EventGuest participation, CancellationToken ct)
    {
        var rows = await BuildAsync(participation.Event, ct, participation.Id);
        return rows.FirstOrDefault();
    }

    /// <summary>
    /// Builds the whole mission's checklist in a fixed number of queries —
    /// one per booking type plus one for waivers — rather than per delegate.
    /// </summary>
    private async Task<List<ReadinessResponse>> BuildAsync(
        Event mission, CancellationToken ct, int? onlyParticipationId = null)
    {
        var rosterQuery = _unitOfWork.EventGuests.QueryNoTracking().Where(eg => eg.EventId == mission.Id);
        if (onlyParticipationId.HasValue)
            rosterQuery = rosterQuery.Where(eg => eg.Id == onlyParticipationId.Value);

        var roster = await rosterQuery
            .OrderBy(eg => eg.Subgroup).ThenBy(eg => eg.Guest.FirstName)
            .Select(eg => new
            {
                eg.Id,
                eg.PublicId,
                PersonId = eg.Guest.PublicId,
                FullName = (eg.Guest.FirstName + " " + eg.Guest.LastName).Trim(),
                MissionRoleName = eg.MissionRole != null ? eg.MissionRole.Name : null,
                eg.Subgroup,
                eg.VisaRequired,
                eg.VisaStatus,
                eg.Guest.PassportNumber,
                eg.Guest.PassportExpiry,
            })
            .ToListAsync(ct);

        if (roster.Count == 0) return new List<ReadinessResponse>();

        var ids = roster.Select(r => r.Id).ToList();

        var withFlight = (await _unitOfWork.Flights.QueryNoTracking()
            .Where(f => ids.Contains(f.EventGuestId)).Select(f => f.EventGuestId).Distinct().ToListAsync(ct)).ToHashSet();
        var withHotel = (await _unitOfWork.Accommodations.QueryNoTracking()
            .Where(a => ids.Contains(a.EventGuestId)).Select(a => a.EventGuestId).Distinct().ToListAsync(ct)).ToHashSet();
        var withTransport = (await _unitOfWork.Transports.QueryNoTracking()
            .Where(t => ids.Contains(t.EventGuestId)).Select(t => t.EventGuestId).Distinct().ToListAsync(ct)).ToHashSet();

        var waivers = (await _unitOfWork.ReadinessWaivers.QueryNoTracking()
            .Where(w => ids.Contains(w.EventGuestId))
            .Select(w => new
            {
                w.EventGuestId,
                w.ItemKey,
                w.PublicId,
                w.Reason,
                w.WaivedAt,
                WaivedByName = w.WaivedByUser != null
                    ? (w.WaivedByUser.FirstName + " " + w.WaivedByUser.LastName).Trim()
                    : null,
            })
            .ToListAsync(ct))
            .ToDictionary(w => (w.EventGuestId, w.ItemKey));

        // Passport is judged against the mission start, not today: it must be
        // valid when they travel, which is the only date that matters.
        var travelDate = mission.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var result = new List<ReadinessResponse>(roster.Count);
        foreach (var r in roster)
        {
            var raw = new List<(string Key, bool Met, string Detail)>
            {
                (ReadinessItems.Passport, PassportMet(r.PassportNumber, r.PassportExpiry, travelDate),
                    PassportDetail(r.PassportNumber, r.PassportExpiry, travelDate)),

                (ReadinessItems.Visa, VisaMet(r.VisaRequired, r.VisaStatus),
                    !r.VisaRequired ? "Not required" : r.VisaStatus ?? "pending"),

                (ReadinessItems.Flight, withFlight.Contains(r.Id),
                    withFlight.Contains(r.Id) ? "Booked" : "No flight booked"),

                (ReadinessItems.Accommodation, withHotel.Contains(r.Id),
                    withHotel.Contains(r.Id) ? "Assigned" : "No room assigned"),

                (ReadinessItems.Transport, withTransport.Contains(r.Id),
                    withTransport.Contains(r.Id) ? "Arranged" : "No transport arranged"),
            };

            var items = new List<ReadinessItemState>(raw.Count);
            foreach (var (key, met, detail) in raw)
            {
                if (met)
                {
                    items.Add(new ReadinessItemState { Key = key, Status = Met, Detail = detail });
                    continue;
                }

                if (waivers.TryGetValue((r.Id, key), out var w))
                {
                    items.Add(new ReadinessItemState
                    {
                        Key = key,
                        Status = Waived,
                        Detail = detail,
                        WaiverId = w.PublicId,
                        WaiverReason = w.Reason,
                        WaivedByName = w.WaivedByName,
                        WaivedAt = w.WaivedAt,
                    });
                    continue;
                }

                items.Add(new ReadinessItemState { Key = key, Status = Missing, Detail = detail });
            }

            result.Add(new ReadinessResponse
            {
                Id = r.PublicId,
                PersonId = r.PersonId,
                FullName = r.FullName,
                MissionRoleName = r.MissionRoleName,
                Subgroup = r.Subgroup,
                TravelReady = items.All(i => i.Status != Missing),
                ReadyWithWaivers = items.All(i => i.Status != Missing) && items.Any(i => i.Status == Waived),
                Items = items,
            });
        }

        return result;
    }

    private static bool PassportMet(string number, DateOnly? expiry, DateOnly travelDate)
        => !string.IsNullOrWhiteSpace(number) && expiry.HasValue && expiry.Value >= travelDate;

    private static string PassportDetail(string number, DateOnly? expiry, DateOnly travelDate)
    {
        if (string.IsNullOrWhiteSpace(number)) return "No passport on file";
        if (!expiry.HasValue) return "No expiry date on file";
        return expiry.Value < travelDate
            ? $"Expired {expiry.Value:yyyy-MM-dd}, before travel"
            : $"Valid to {expiry.Value:yyyy-MM-dd}";
    }

    /// <summary>A delegate who needs no visa passes automatically — that is how
    /// "not required" is expressed, since the status set has no such value.
    /// Both active and expiring-soon are valid documents.</summary>
    private static bool VisaMet(bool required, string status)
        => !required
           || status == VisaStatuses.Active
           || status == VisaStatuses.ExpiringSoon;
}
