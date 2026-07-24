using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Event;

namespace Core.Interfaces.Services;

public interface IEventService
{
    Task<ApiResponse<PaginatedResponse<EventResponse>>> GetEventsAsync(PagedRequest request, string status, CancellationToken ct = default);
    Task<ApiResponse<EventResponse>> GetEventByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<EventResponse>> CreateEventAsync(CreateEventRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<EventResponse>> UpdateEventAsync(Guid id, UpdateEventRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<EventResponse>> UpdateStatusAsync(Guid id, string status, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteEventAsync(Guid id, int userId, CancellationToken ct = default);

    Task<ApiResponse<List<SessionResponse>>> GetSessionsAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<SessionResponse>> AddSessionAsync(Guid eventId, CreateSessionRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<SessionResponse>> UpdateSessionAsync(Guid eventId, Guid sessionId, UpdateSessionRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteSessionAsync(Guid eventId, Guid sessionId, int userId, CancellationToken ct = default);
}
