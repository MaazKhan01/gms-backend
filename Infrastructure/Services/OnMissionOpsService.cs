using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Constants.Notification;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.OnMissionOps;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 7 — the coordinator's ops desk while the delegation is on the ground.
///
/// A gathering notification is an OUTBOX entry, not a delivery mechanism: the
/// message fans out through the existing notification machinery (one row per
/// delegate, pushed to their devices), and this table records what was sent to
/// the group. The existing Notifications table answers "what did this person
/// receive"; the ops screen needs "what did I send".
/// </summary>
public class OnMissionOpsService(
    IUnitOfWork _unitOfWork,
    INotificationManagerService _notifications,
    ILogger<OnMissionOpsService> _logger) : IOnMissionOpsService
{
    private const int MaxMessageLength = 1000;

    public async Task<ApiResponse<List<SubgroupHeadcountResponse>>> GetSubgroupsAsync(
        Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<SubgroupHeadcountResponse>>.NotFoundResponse("Mission not found.");

        var groups = await _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.EventId == mission.Id)
            .GroupBy(eg => eg.Subgroup)
            .Select(g => new SubgroupHeadcountResponse { Subgroup = g.Key, Headcount = g.Count() })
            .ToListAsync(ct);

        // Unassigned delegates last; the rest alphabetically.
        return ApiResponse<List<SubgroupHeadcountResponse>>.SuccessResponse(
            groups.OrderBy(g => string.IsNullOrWhiteSpace(g.Subgroup)).ThenBy(g => g.Subgroup).ToList());
    }

    public async Task<ApiResponse<List<GatheringNotificationResponse>>> GetNotificationsAsync(
        Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<GatheringNotificationResponse>>.NotFoundResponse("Mission not found.");

        var items = await _unitOfWork.GatheringNotifications.QueryNoTracking()
            .Where(n => n.EventId == mission.Id)
            .OrderByDescending(n => n.SentAt)
            .Select(n => new GatheringNotificationResponse
            {
                Id = n.PublicId,
                Message = n.Message,
                Subgroup = n.Subgroup,
                RecipientCount = n.RecipientCount,
                SentByName = n.SentByUser != null
                    ? (n.SentByUser.FirstName + " " + n.SentByUser.LastName).Trim()
                    : null,
                SentAt = n.SentAt,
            })
            .ToListAsync(ct);

        return ApiResponse<List<GatheringNotificationResponse>>.SuccessResponse(items);
    }

    public async Task<ApiResponse<GatheringNotificationResponse>> SendNotificationAsync(
        SendGatheringNotificationRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null || request.EventId == Guid.Empty)
            return ApiResponse<GatheringNotificationResponse>.ErrorResponse("Mission id is required.");

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return ApiResponse<GatheringNotificationResponse>.ErrorResponse("Message is required.");
        if (message.Length > MaxMessageLength)
            return ApiResponse<GatheringNotificationResponse>.ErrorResponse(
                $"Message cannot exceed {MaxMessageLength} characters.");

        var mission = await FindMissionAsync(request.EventId, ct);
        if (mission == null)
            return ApiResponse<GatheringNotificationResponse>.NotFoundResponse("Mission not found.");

        var subgroup = string.IsNullOrWhiteSpace(request.Subgroup) ? null : request.Subgroup.Trim();

        var roster = _unitOfWork.EventGuests.QueryNoTracking().Where(eg => eg.EventId == mission.Id);
        if (subgroup != null) roster = roster.Where(eg => eg.Subgroup == subgroup);

        // Guest.Id, not EventGuest.Id — the notification layer targets the person,
        // and both ids come from the same sequence, so the wrong one would quietly
        // notify an unrelated person instead of failing.
        var guestIds = await roster.Select(eg => eg.GuestId).Distinct().ToListAsync(ct);

        if (guestIds.Count == 0)
            return ApiResponse<GatheringNotificationResponse>.ErrorResponse(
                subgroup == null
                    ? "This mission has no delegates to notify."
                    : $"No delegates are in subgroup '{subgroup}'.",
                "NO_RECIPIENTS");

        var content = NotificationTemplates.Build(
            NotificationTemplates.MissionGatheringNotice,
            new Dictionary<string, string>
            {
                ["message"] = message,
                ["missionId"] = mission.PublicId.ToString(),
                ["subgroup"] = subgroup,
            });

        int delivered;
        try
        {
            delivered = (await _notifications.SendToGuestsAsync(guestIds, content, ct)).Count;
        }
        catch (Exception ex)
        {
            // The outbox row is the point of the operation; a push that fails is
            // worth reporting rather than silently recording a send that never went.
            _logger.LogError(ex, "Gathering notification fan-out failed for mission {MissionId}", mission.Id);
            return ApiResponse<GatheringNotificationResponse>.ServerErrorResponse(
                "The message could not be delivered. Nothing was recorded.");
        }

        var row = new GatheringNotification
        {
            EventId = mission.Id,
            Message = message,
            Subgroup = subgroup,
            // What actually went out, not what we aimed at.
            RecipientCount = delivered,
            SentBy = userId == 0 ? null : userId,
            SentAt = DateTime.UtcNow,
        };
        row.SetCreationAudit(userId);

        await _unitOfWork.GatheringNotifications.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var sender = userId == 0 ? null : await _unitOfWork.Users.GetByIdAsync(userId, ct);

        return ApiResponse<GatheringNotificationResponse>.SuccessResponse(
            new GatheringNotificationResponse
            {
                Id = row.PublicId,
                Message = row.Message,
                Subgroup = row.Subgroup,
                RecipientCount = row.RecipientCount,
                SentByName = sender == null ? null : (sender.FirstName + " " + sender.LastName).Trim(),
                SentAt = row.SentAt,
            },
            $"Sent to {delivered} delegate(s).");
    }

    private Task<Event> FindMissionAsync(Guid eventId, CancellationToken ct)
        => eventId == Guid.Empty
            ? Task.FromResult<Event>(null)
            : _unitOfWork.Events.QueryNoTracking().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
}
