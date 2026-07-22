using Core.ViewModel.Common;
using Core.ViewModel.Dashboard;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IDashboardService
    {
        Task<ApiResponse<GetDashboardResponse>> GetDashboardAsync(Guid eventId, CancellationToken ct);
    }
}
