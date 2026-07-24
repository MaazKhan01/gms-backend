using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Common.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;

namespace Infrastructure.Services;

public class NotificationService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    ILogger<NotificationService> _logger,
    ICurrentUser _currentUser) : INotificationService
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
            }

            return ApiResponse<bool>.SuccessResponse(true, "Notification marked as read");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as read", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while marking the notification as read");
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

            _unitOfWork.Notifications.Remove(notification);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Notification deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {Id}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the notification");
        }
    }
}
