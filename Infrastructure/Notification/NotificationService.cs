using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Common.Interfaces;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;

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
}
