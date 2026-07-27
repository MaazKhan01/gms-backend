using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Noification;
using Core.ViewModel.VipApp;
using DomainPersistence.Entities;

// Matches the existing convention of this folder — no namespace declared.
//
// The single reusable notification-dispatch engine — see INotificationManagerService
// for the contract. Every Send*/Broadcast* method: (1) resolves recipients,
// (2) bulk-persists one Notification/GuestNotification row per recipient,
// (3) best-effort pushes each over SignalR via IPushNotificationProvider.
// Callers that used to hand-roll steps 2+3 (e.g. SupportChatService) should
// call in here instead — see SupportChatService.NotifyAdminsAsync/NotifyGuestAsync.
public class NotificationManagerService(
    IUnitOfWork _unitOfWork,
    IPushNotificationProvider _pushNotificationProvider,
    IRealTimeAlertService _realTimeAlertService,
    ILogger<NotificationManagerService> _logger
) : INotificationManagerService
{
    public async Task SendAlert(SendAlertRequest input)
    {
        try
        {
            if (!string.IsNullOrEmpty(input.Topic))
            {
                // Ephemeral all-clients broadcast — not persisted, doesn't fit the
                // per-recipient Notification model (no single owning user).
                await _realTimeAlertService.SendToAllAsync(
                    topic: input.Topic,
                    title: input.Title,
                    message: input.Message,
                    data: input.Metadata
                ).ConfigureAwait(false);
            }

            if (input.UserId.HasValue)
            {
                // input.UserId is the public Guid; resolve it to the internal int FK.
                var user = await _unitOfWork.Users.GetByPublicIdAsync(input.UserId.Value).ConfigureAwait(false);
                if (user != null)
                {
                    await SendToUserAsync(user.Id, new NotificationContent
                    {
                        Title = input.Title ?? string.Empty,
                        Message = input.Message,
                        Type = input.NotificationTemplateCode,
                        RedirectUrl = input.Url,
                        Data = input.Metadata
                    }).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending alert");
        }
    }

    public async Task<NotificationResponse> SendToUserAsync(int userId, NotificationContent content, CancellationToken ct = default)
    {
        var results = await SendToUsersAsync(new[] { userId }, content, ct).ConfigureAwait(false);
        return results.Count > 0 ? results[0] : null;
    }

    public async Task<IReadOnlyList<NotificationResponse>> SendToUsersAsync(IEnumerable<int> userIds, NotificationContent content, CancellationToken ct = default)
    {
        var ids = (userIds ?? Enumerable.Empty<int>()).Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<NotificationResponse>();

        var recipients = await _unitOfWork.Users.Query()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.PublicId })
            .ToListAsync(ct).ConfigureAwait(false);

        return await PersistAndPushAsync(recipients.Select(r => (r.Id, r.PublicId)), content, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationResponse>> SendToRoleAsync(string roleCode, NotificationContent content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return Array.Empty<NotificationResponse>();

        var recipients = await _unitOfWork.Users.Query()
            .Where(u => u.IsActive && u.Role != null && u.Role.Code == roleCode)
            .Select(u => new { u.Id, u.PublicId })
            .ToListAsync(ct).ConfigureAwait(false);

        return await PersistAndPushAsync(recipients.Select(r => (r.Id, r.PublicId)), content, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationResponse>> SendToPermissionAsync(string permissionCode, NotificationContent content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return Array.Empty<NotificationResponse>();

        // Same "every holder of this permission" shape SupportChatService used
        // to hand-roll for SupportChatManage — now reusable for any permission.
        var recipients = await _unitOfWork.Users.Query()
            .Where(u => u.IsActive && u.Role != null &&
                        u.Role.RolePermissions.Any(rp => rp.Permission.Code == permissionCode))
            .Select(u => new { u.Id, u.PublicId })
            .ToListAsync(ct).ConfigureAwait(false);

        return await PersistAndPushAsync(recipients.Select(r => (r.Id, r.PublicId)), content, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationResponse>> BroadcastToAllUsersAsync(NotificationContent content, CancellationToken ct = default)
    {
        var recipients = await _unitOfWork.Users.Query()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.PublicId })
            .ToListAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Broadcasting notification '{Title}' to {Count} active users", content.Title, recipients.Count);

        return await PersistAndPushAsync(recipients.Select(r => (r.Id, r.PublicId)), content, ct).ConfigureAwait(false);
    }

    public async Task<GuestNotificationResponse> SendToGuestAsync(int guestId, NotificationContent content, CancellationToken ct = default)
    {
        var results = await SendToGuestsAsync(new[] { guestId }, content, ct).ConfigureAwait(false);
        return results.Count > 0 ? results[0] : null;
    }

    public async Task<IReadOnlyList<GuestNotificationResponse>> BroadcastToAllGuestsAsync(NotificationContent content, CancellationToken ct = default)
    {
        var ids = await _unitOfWork.Guests.Query()
            .Where(g => g.NotificationsEnabled)
            .Select(g => g.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Broadcasting notification '{Title}' to {Count} guests", content.Title, ids.Count);

        return await SendToGuestsAsync(ids, content, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GuestNotificationResponse>> SendToGuestsAsync(IEnumerable<int> guestIds, NotificationContent content, CancellationToken ct = default)
    {
        var ids = (guestIds ?? Enumerable.Empty<int>()).Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<GuestNotificationResponse>();

        var dataJson = SerializeData(content.Data);
        var entities = ids.Select(id => new GuestNotification
        {
            GuestId = id,
            Title = content.Title ?? string.Empty,
            Message = content.Message,
            Type = content.Type,
            RedirectUrl = content.RedirectUrl,
            Data = dataJson,
            Read = false
        }).ToList();

        await _unitOfWork.GuestNotifications.AddRangeAsync(entities, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        var responses = entities.Select(n => new GuestNotificationResponse
        {
            Id = n.PublicId,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            Read = n.Read,
            CreatedAt = n.CreatedAt,
            RedirectUrl = n.RedirectUrl,
            Data = n.Data
        }).ToList();

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var response = responses[i];
            try
            {
                await _pushNotificationProvider.SendAsync(new PushNotificationPayload
                {
                    RecipientType = NotificationRecipientType.Guest,
                    GuestId = entity.GuestId,
                    Title = entity.Title,
                    Body = entity.Message,
                    Topic = content.Topic ?? RealtimeTopics.NotificationNew,
                    Data = content.Data,
                    Payload = response
                }, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing notification to guest {GuestId}", entity.GuestId);
            }
        }

        return responses;
    }

    // ------------------------------------------------------------------
    // Shared persist-then-push for User recipients.
    // ------------------------------------------------------------------
    private async Task<IReadOnlyList<NotificationResponse>> PersistAndPushAsync(
        IEnumerable<(int Id, Guid PublicId)> recipients, NotificationContent content, CancellationToken ct)
    {
        var recipientList = recipients.ToList();
        if (recipientList.Count == 0) return Array.Empty<NotificationResponse>();

        var dataJson = SerializeData(content.Data);
        var entities = recipientList.Select(r => new Notification
        {
            UserId = r.Id,
            Title = content.Title ?? string.Empty,
            Message = content.Message,
            Type = content.Type,
            RedirectUrl = content.RedirectUrl,
            Data = dataJson,
            Read = false
        }).ToList();

        await _unitOfWork.Notifications.AddRangeAsync(entities, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        var responses = new List<NotificationResponse>(entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            responses.Add(new NotificationResponse
            {
                Id = entity.PublicId,
                UserId = recipientList[i].PublicId,
                Title = entity.Title,
                Message = entity.Message,
                Type = entity.Type,
                Read = entity.Read,
                CreatedAt = entity.CreatedAt,
                RedirectUrl = entity.RedirectUrl,
                Data = entity.Data
            });
        }

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var response = responses[i];
            try
            {
                await _pushNotificationProvider.SendAsync(new PushNotificationPayload
                {
                    RecipientType = NotificationRecipientType.User,
                    UserId = entity.UserId,
                    Title = entity.Title,
                    Body = entity.Message,
                    Topic = content.Topic ?? RealtimeTopics.NotificationNew,
                    Data = content.Data,
                    Payload = response
                }, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing notification to user {UserId}", entity.UserId);
            }
        }

        return responses;
    }

    private static string SerializeData(IDictionary<string, string> data)
        => data == null || data.Count == 0 ? null : JsonSerializer.Serialize(data);
}
