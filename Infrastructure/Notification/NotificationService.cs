using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Common.Interfaces;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;
using Core.ViewModel.VipApp;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class NotificationService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    ILogger<NotificationService> _logger,
    ICurrentUser _currentUser,
    INotificationManagerService _notificationManagerService,
    IRealTimeAlertService _realTimeAlertService) : INotificationService
{
    public async Task<ApiResponse<PaginatedResponse<NotificationResponse>>> GetAllNotificationAsync(
        NotificationPagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var userId = _currentUser.UserId;
            var query = _unitOfWork.Notifications.Query()
                .Where(n => n.UserId == userId && (request.IsRead || n.Read != true));

            if (!string.IsNullOrEmpty(request.SearchTerm))
                query = query.Where(n => n.Title.Contains(request.SearchTerm) || n.Message.Contains(request.SearchTerm));

            if (!string.IsNullOrEmpty(request.Type))
                query = query.Where(n => n.Type == request.Type);

            if (request.FromDate.HasValue)
                query = query.Where(n => n.CreatedAt >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(n => n.CreatedAt <= request.ToDate.Value);

            var totalCount = await query.CountAsync(ct);
            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var response = new PaginatedResponse<NotificationResponse>(
                _mapper.Map<List<NotificationResponse>>(notifications), totalCount, request.PageNumber, request.PageSize);

            return ApiResponse<PaginatedResponse<NotificationResponse>>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications");
            return ApiResponse<PaginatedResponse<NotificationResponse>>.ServerErrorResponse("An error occurred while fetching notifications");
        }
    }

    public async Task<ApiResponse<NotificationResponse>> GetNotificationByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var userId = _currentUser.UserId;
            var notification = await _unitOfWork.Notifications.Query()
                .FirstOrDefaultAsync(n => n.PublicId == id && n.UserId == userId, ct);

            if (notification == null)
                return ApiResponse<NotificationResponse>.NotFoundResponse("Notification not found");

            return ApiResponse<NotificationResponse>.SuccessResponse(_mapper.Map<NotificationResponse>(notification));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification {Id}", id);
            return ApiResponse<NotificationResponse>.ServerErrorResponse("An error occurred while fetching the notification");
        }
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync(CancellationToken ct = default)
    {
        try
        {
            var userId = _currentUser.UserId;
            var notifications = await _unitOfWork.Notifications.Query()
                .Where(n => n.UserId == userId && n.Read != true)
                .ToListAsync(ct);

            foreach (var n in notifications)
            {
                n.Read = true;
                _unitOfWork.Notifications.Update(n);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await PushUnreadCountAsync(userId, ct);
            return ApiResponse<bool>.SuccessResponse(true, "All notifications marked as read");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while marking notifications as read");
        }
    }

    public async Task<ApiResponse<bool>> MarkSingleAsReadAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var userId = _currentUser.UserId;
            var notification = await _unitOfWork.Notifications.Query()
                .FirstOrDefaultAsync(n => n.PublicId == id && n.UserId == userId, ct);

            if (notification == null)
                return ApiResponse<bool>.NotFoundResponse("Notification not found");

            if (notification.Read != true)
            {
                notification.Read = true;
                _unitOfWork.Notifications.Update(notification);
                await _unitOfWork.SaveChangesAsync(ct);
                await PushUnreadCountAsync(userId, ct);
            }

            return ApiResponse<bool>.SuccessResponse(true, "Notification marked as read");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as read", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while marking the notification as read");
        }
    }

    public async Task<ApiResponse<bool>> MarkSingleAsUnreadAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var userId = _currentUser.UserId;
            var notification = await _unitOfWork.Notifications.Query()
                .FirstOrDefaultAsync(n => n.PublicId == id && n.UserId == userId, ct);

            if (notification == null)
                return ApiResponse<bool>.NotFoundResponse("Notification not found");

            if (notification.Read != false)
            {
                notification.Read = false;
                _unitOfWork.Notifications.Update(notification);
                await _unitOfWork.SaveChangesAsync(ct);
                await PushUnreadCountAsync(userId, ct);
            }

            return ApiResponse<bool>.SuccessResponse(true, "Notification marked as unread");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as unread", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while marking the notification as unread");
        }
    }

    public async Task<ApiResponse<int>> GetTotalCountAsync(CancellationToken ct = default)
    {
        try
        {
            var userId = _currentUser.UserId;
            var count = await _unitOfWork.Notifications.Query()
                .CountAsync(n => n.UserId == userId && n.Read != true, ct);

            return ApiResponse<int>.SuccessResponse(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification count");
            return ApiResponse<int>.ServerErrorResponse("An error occurred while getting notification count");
        }
    }

    public async Task<ApiResponse<bool>> DeleteNotificationAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var userId = _currentUser.UserId;
            var notification = await _unitOfWork.Notifications.Query()
                .FirstOrDefaultAsync(n => n.PublicId == id && n.UserId == userId, ct);

            if (notification == null)
                return ApiResponse<bool>.NotFoundResponse("Notification not found");

            var wasUnread = notification.Read != true;

            // Notification now inherits AuditEntity — Remove() is intercepted into a
            // soft delete (see AuditInterceptor), so history/audit trail is preserved.
            _unitOfWork.Notifications.Remove(notification);
            await _unitOfWork.SaveChangesAsync(ct);

            if (wasUnread)
                await PushUnreadCountAsync(userId, ct);

            return ApiResponse<bool>.SuccessResponse(true, "Notification deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {Id}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the notification");
        }
    }

    public async Task<ApiResponse<SendNotificationResult>> SendNotificationAsync(
        SendNotificationRequest request, CancellationToken ct = default)
    {
        try
        {
            var targetCount = new[]
            {
                request.UserIds is { Count: > 0 },
                !string.IsNullOrWhiteSpace(request.RoleCode),
                !string.IsNullOrWhiteSpace(request.PermissionCode),
                request.Broadcast,
                request.GuestIds is { Count: > 0 },
                request.BroadcastGuests
            }.Count(isSet => isSet);

            if (targetCount != 1)
                return ApiResponse<SendNotificationResult>.ErrorResponse(
                    "Specify exactly one target: userIds, roleCode, permissionCode, broadcast, guestIds, or broadcastGuests");

            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiResponse<SendNotificationResult>.ErrorResponse("Title is required");

            var content = new NotificationContent
            {
                Title = request.Title,
                Message = request.Message,
                Type = request.Type,
                RedirectUrl = request.RedirectUrl,
                Data = request.Data
            };

            string targetType;
            int recipientCount;

            if (request.UserIds is { Count: > 0 })
            {
                var internalIds = await _unitOfWork.Users.Query()
                    .Where(u => request.UserIds.Contains(u.PublicId))
                    .Select(u => u.Id)
                    .ToListAsync(ct);

                if (internalIds.Count == 0)
                    return ApiResponse<SendNotificationResult>.NotFoundResponse("No matching users found");

                recipientCount = (await _notificationManagerService.SendToUsersAsync(internalIds, content, ct)).Count;
                targetType = "Users";
            }
            else if (!string.IsNullOrWhiteSpace(request.RoleCode))
            {
                recipientCount = (await _notificationManagerService.SendToRoleAsync(request.RoleCode, content, ct)).Count;
                targetType = "Role";
            }
            else if (!string.IsNullOrWhiteSpace(request.PermissionCode))
            {
                recipientCount = (await _notificationManagerService.SendToPermissionAsync(request.PermissionCode, content, ct)).Count;
                targetType = "Permission";
            }
            else if (request.Broadcast)
            {
                recipientCount = (await _notificationManagerService.BroadcastToAllUsersAsync(content, ct)).Count;
                targetType = "Broadcast";
            }
            else if (request.GuestIds is { Count: > 0 })
            {
                // Guests aren't Users — resolve against the Guests table instead.
                var internalGuestIds = await _unitOfWork.Guests.Query()
                    .Where(g => request.GuestIds.Contains(g.PublicId))
                    .Select(g => g.Id)
                    .ToListAsync(ct);

                if (internalGuestIds.Count == 0)
                    return ApiResponse<SendNotificationResult>.NotFoundResponse("No matching guests found");

                recipientCount = (await _notificationManagerService.SendToGuestsAsync(internalGuestIds, content, ct)).Count;
                targetType = "Guests";
            }
            else
            {
                recipientCount = (await _notificationManagerService.BroadcastToAllGuestsAsync(content, ct)).Count;
                targetType = "BroadcastGuests";
            }

            return ApiResponse<SendNotificationResult>.SuccessResponse(
                new SendNotificationResult { RecipientCount = recipientCount, TargetType = targetType },
                $"Notification sent to {recipientCount} recipient(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification");
            return ApiResponse<SendNotificationResult>.ServerErrorResponse("An error occurred while sending the notification");
        }
    }

    // Best-effort — keeps the badge in sync across open tabs/devices for the
    // user who just changed their own read state. Never fails the caller.
    private async Task PushUnreadCountAsync(int userId, CancellationToken ct)
    {
        try
        {
            var count = await _unitOfWork.Notifications.Query()
                .CountAsync(n => n.UserId == userId && n.Read != true, ct);

            await _realTimeAlertService.SendToUserAsync(
                RealtimeTopics.NotificationCountChanged, userId.ToString(), null, null,
                new { unreadCount = count }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing unread count update for user {UserId}", userId);
        }
    }

    // ============================================================
    // Guest-facing (VIP app, OTP login) — moved here from VipAppService.
    // Guests are Users now (Guest.UserId) — these read/write the same
    // Notifications/Devices tables as everyone else, just resolved via the
    // guest's linked UserId instead of ICurrentUser. Kept as their own methods
    // (rather than folded into the User-facing ones above) because the caller
    // is still ICurrentGuest, not ICurrentUser — same precedent as support chat.
    // ============================================================

    public async Task<ApiResponse<List<GuestNotificationResponse>>> GetGuestNotificationsAsync(
        int guestId, PagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var guestUserId = await ResolveGuestUserIdAsync(guestId, ct);
            if (guestUserId is null)
                return ApiResponse<List<GuestNotificationResponse>>.SuccessResponse(new());

            var page = request?.PageNumber > 0 ? request.PageNumber : 1;
            var size = request?.PageSize > 0 ? request.PageSize : 20;

            var items = await _unitOfWork.Notifications.QueryNoTracking()
                .Where(n => n.UserId == guestUserId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * size).Take(size).ToListAsync(ct);

            var data = items.Select(n => new GuestNotificationResponse
            {
                Id = n.PublicId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                Read = n.Read == true,
                CreatedAt = n.CreatedAt,
                RedirectUrl = n.RedirectUrl,
                Data = n.Data
            }).ToList();

            return ApiResponse<List<GuestNotificationResponse>>.SuccessResponse(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications for guest {GuestId}", guestId);
            return ApiResponse<List<GuestNotificationResponse>>.ServerErrorResponse("An error occurred while fetching notifications");
        }
    }

    public async Task<ApiResponse<int>> GetGuestUnreadCountAsync(int guestId, CancellationToken ct = default)
    {
        try
        {
            var guestUserId = await ResolveGuestUserIdAsync(guestId, ct);
            if (guestUserId is null)
                return ApiResponse<int>.SuccessResponse(0);

            var count = await _unitOfWork.Notifications.CountAsync(n => n.UserId == guestUserId && n.Read != true, ct);
            return ApiResponse<int>.SuccessResponse(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for guest {GuestId}", guestId);
            return ApiResponse<int>.ServerErrorResponse("An error occurred while getting notification count");
        }
    }

    public async Task<ApiResponse<bool>> MarkGuestNotificationReadAsync(int guestId, Guid notificationId, CancellationToken ct = default)
    {
        try
        {
            var guestUserId = await ResolveGuestUserIdAsync(guestId, ct);
            if (guestUserId is null)
                return ApiResponse<bool>.NotFoundResponse("Notification not found");

            var notification = await _unitOfWork.Notifications
                .FindFirstOrDefaultAsync(n => n.PublicId == notificationId && n.UserId == guestUserId, ct);

            if (notification == null)
                return ApiResponse<bool>.NotFoundResponse("Notification not found");

            if (notification.Read != true)
            {
                notification.Read = true;
                _unitOfWork.Notifications.Update(notification);
                await _unitOfWork.SaveChangesAsync(ct);
                await PushGuestUnreadCountAsync(guestUserId.Value, ct);
            }

            return ApiResponse<bool>.SuccessResponse(true, "Notification marked as read");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as read for guest {GuestId}", notificationId, guestId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while marking the notification as read");
        }
    }

    public async Task<ApiResponse<bool>> MarkAllGuestNotificationsReadAsync(int guestId, CancellationToken ct = default)
    {
        try
        {
            var guestUserId = await ResolveGuestUserIdAsync(guestId, ct);
            if (guestUserId is null)
                return ApiResponse<bool>.SuccessResponse(true, "0 marked read");

            var unread = (await _unitOfWork.Notifications.FindAsync(n => n.UserId == guestUserId && n.Read != true, ct)).ToList();

            if (unread.Count > 0)
            {
                foreach (var n in unread) n.Read = true;
                _unitOfWork.Notifications.UpdateRange(unread);
                await _unitOfWork.SaveChangesAsync(ct);
                await PushGuestUnreadCountAsync(guestUserId.Value, ct);
            }

            return ApiResponse<bool>.SuccessResponse(true, $"{unread.Count} marked read");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for guest {GuestId}", guestId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while marking notifications as read");
        }
    }

    // Kept as the guest-facing route (POST /notifications/guest/devices) for
    // backward compatibility with the existing VIP app build — internally just
    // an alias for the shared RegisterDeviceAsync (see below), keyed by the
    // guest's linked UserId instead of ICurrentUser.
    public async Task<ApiResponse<bool>> RegisterGuestDeviceAsync(int guestId, RegisterDeviceRequest request, CancellationToken ct = default)
    {
        var guestUserId = await ResolveGuestUserIdAsync(guestId, ct);
        if (guestUserId is null)
            return ApiResponse<bool>.NotFoundResponse("Guest not found");

        return await RegisterDeviceForUserAsync(guestUserId.Value, request, ct);
    }

    public async Task<ApiResponse<bool>> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct = default)
        => await RegisterDeviceForUserAsync(_currentUser.UserId, request, ct);

    public async Task<ApiResponse<DeviceResponse>> UpdateDeviceAsync(UpdateDeviceRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Token))
                return ApiResponse<DeviceResponse>.ErrorResponse("Device token is required");

            var device = await _unitOfWork.Devices
                .FindFirstOrDefaultAsync(d => d.Token == request.Token && d.UserId == _currentUser.UserId, ct);
            if (device is null)
                return ApiResponse<DeviceResponse>.NotFoundResponse("No device registered with this token");

            if (request.DeviceIdentifier != null) device.DeviceIdentifier = request.DeviceIdentifier;
            if (request.Platform != null) device.Platform = request.Platform;
            if (request.DeviceModel != null) device.DeviceModel = request.DeviceModel;
            if (request.OsVersion != null) device.OsVersion = request.OsVersion;
            if (request.AppVersion != null) device.AppVersion = request.AppVersion;
            if (request.NotificationsEnabled.HasValue) device.NotificationsEnabled = request.NotificationsEnabled.Value;
            device.LastActiveAt = DateTime.UtcNow;

            _unitOfWork.Devices.Update(device);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<DeviceResponse>.SuccessResponse(MapDevice(device), "Device updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device");
            return ApiResponse<DeviceResponse>.ServerErrorResponse("An error occurred while updating the device");
        }
    }

    public async Task<ApiResponse<bool>> DeregisterDeviceAsync(string token, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return ApiResponse<bool>.ErrorResponse("Device token is required");

            var device = await _unitOfWork.Devices.FindFirstOrDefaultAsync(d => d.Token == token && d.UserId == _currentUser.UserId, ct);
            if (device is null)
                return ApiResponse<bool>.SuccessResponse(true, "Nothing to deregister");

            device.IsActive = false;
            _unitOfWork.Devices.Update(device);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, "Device deregistered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deregistering device");
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deregistering the device");
        }
    }

    public async Task<ApiResponse<DeviceResponse>> GetMyDeviceByTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return ApiResponse<DeviceResponse>.ErrorResponse("Device token is required");

            var device = await _unitOfWork.Devices.QueryNoTracking()
                .FirstOrDefaultAsync(d => d.Token == token && d.UserId == _currentUser.UserId, ct);

            return device is null
                ? ApiResponse<DeviceResponse>.NotFoundResponse("No device registered with this token")
                : ApiResponse<DeviceResponse>.SuccessResponse(MapDevice(device));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking device token");
            return ApiResponse<DeviceResponse>.ServerErrorResponse("An error occurred while checking the device token");
        }
    }

    public async Task<ApiResponse<List<DeviceResponse>>> GetMyDevicesAsync(CancellationToken ct = default)
    {
        try
        {
            var devices = await _unitOfWork.Devices.QueryNoTracking()
                .Where(d => d.UserId == _currentUser.UserId)
                .OrderByDescending(d => d.LastActiveAt)
                .ToListAsync(ct);

            return ApiResponse<List<DeviceResponse>>.SuccessResponse(devices.Select(MapDevice).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching devices for user {UserId}", _currentUser.UserId);
            return ApiResponse<List<DeviceResponse>>.ServerErrorResponse("An error occurred while fetching devices");
        }
    }

    private static DeviceResponse MapDevice(Device d) => new()
    {
        Id = d.PublicId,
        Token = d.Token,
        Platform = d.Platform,
        DeviceIdentifier = d.DeviceIdentifier,
        DeviceModel = d.DeviceModel,
        OsVersion = d.OsVersion,
        AppVersion = d.AppVersion,
        NotificationsEnabled = d.NotificationsEnabled,
        IsActive = d.IsActive,
        LastActiveAt = d.LastActiveAt,
        TokenUpdatedAt = d.TokenUpdatedAt
    };

    // Shared upsert for any authenticated User (staff, driver, or guest) —
    // one row per token, re-pointed at whichever User most recently registered
    // it (a device only ever belongs to one signed-in User at a time).
    private async Task<ApiResponse<bool>> RegisterDeviceForUserAsync(int userId, RegisterDeviceRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Token))
                return ApiResponse<bool>.ErrorResponse("Device token is required");

            var now = DateTime.UtcNow;
            var existing = await _unitOfWork.Devices.FindFirstOrDefaultAsync(d => d.Token == request.Token, ct);
            if (existing is null)
            {
                await _unitOfWork.Devices.AddAsync(new Device
                {
                    UserId = userId,
                    Token = request.Token,
                    Platform = request.Platform,
                    IsActive = true,
                    NotificationsEnabled = true,
                    LastActiveAt = now,
                    TokenUpdatedAt = now,
                }, ct);
            }
            else
            {
                existing.UserId = userId;
                existing.Platform = request.Platform;
                existing.IsActive = true;
                existing.LastActiveAt = now;
                existing.TokenUpdatedAt = now;
                _unitOfWork.Devices.Update(existing);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, "Device registered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering device for user {UserId}", userId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while registering the device");
        }
    }

    private async Task<int?> ResolveGuestUserIdAsync(int guestId, CancellationToken ct)
        => await _unitOfWork.Guests.Query()
            .Where(g => g.Id == guestId)
            .Select(g => (int?)g.UserId)
            .FirstOrDefaultAsync(ct);

    // Mirrors PushUnreadCountAsync — guests now use the same Clients.User(...)
    // targeting as every other User (see ManualNotificationProvider remarks).
    private async Task PushGuestUnreadCountAsync(int guestUserId, CancellationToken ct)
    {
        try
        {
            var count = await _unitOfWork.Notifications.CountAsync(n => n.UserId == guestUserId && n.Read != true, ct);

            await _realTimeAlertService.SendToUserAsync(
                RealtimeTopics.NotificationCountChanged, guestUserId.ToString(), null, null,
                new { unreadCount = count }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing unread count update for guest user {UserId}", guestUserId);
        }
    }
}
