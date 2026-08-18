using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Transportation;

namespace Core.Interfaces.Services;

// Admin side of the transportation module — pre-scheduled rides and the
// eligible-driver pool per participation. Guest on-demand requests go through
// IVipAppService.RequestTransportAsync ("new" status, claimed by a driver).
//
// Every guest id here is an EventGuest.PublicId: transport is event-scoped, and
// the participation is where a ride's event comes from.
public interface ITransportationScheduleService
{
    Task<ApiResponse<ScheduleRow>> CreateScheduleAsync(CreateScheduleRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<List<AssignedDriverDto>>> GetAssignedDriversAsync(Guid eventGuestId, CancellationToken ct = default);

    Task<ApiResponse<List<ScheduleRow>>> GetGuestScheduleAsync(Guid eventGuestId, CancellationToken ct = default);

    Task<ApiResponse<PaginatedResponse<ScheduleRow>>> GetEventScheduleAsync(
        Guid eventId, PagedRequest request, CancellationToken ct = default);

    Task<ApiResponse<bool>> CancelScheduleAsync(Guid transportId, int userId, CancellationToken ct = default);
}
