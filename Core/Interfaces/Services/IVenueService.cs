using Core.ViewModel.Common;
using Core.ViewModel.Venue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IVenueService
    {
        Task<ApiResponse<GetVenueResonse>> CreateVenueAsync(CreateVenueRequest request,Guid userId, CancellationToken ct);
        Task<ApiResponse<GetVenueResonse>> CreateVenueBoxAsync(CreateVenueBoxRequest request, Guid eventId, Guid userId, CancellationToken ct);
        Task<ApiResponse<GetVenueResonse>> GetVenueByIdAsync(Guid venueId, CancellationToken ct);
        Task<ApiResponse<List<GetVenueResonse>>> GetVenuesAsync(CancellationToken ct);
        Task<ApiResponse<bool>> DeleteVenueAsync(Guid id, CancellationToken ct);
        Task<ApiResponse<bool>> DeleteVenueBoxAsync(Guid id, Guid venueId, Guid eventId,Guid sessionId, CancellationToken ct);
    }
}
