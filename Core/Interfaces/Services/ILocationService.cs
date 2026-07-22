using Core.ViewModel.Common;
using Core.ViewModel.Location;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface ILocationService
    {
        Task<ApiResponse<LocationResponse>> CreateLocationAsync(CreateLocationDto request, CancellationToken ct);
    }
}
