using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;

namespace Core.Interfaces.Services;

public interface INotificationService
{
    Task<ApiResponse<PaginatedResponse<NotificationResponse>>> GetAllNotificationAsync(NotificationPagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<NotificationResponse>> GetNotificationByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkAllAsReadAsync(CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkSingleAsReadAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<int>> GetTotalCountAsync(CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteNotificationAsync(Guid id, CancellationToken ct = default);
}
