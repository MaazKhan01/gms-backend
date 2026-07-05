using Core.ViewModel.Common;
using Core.ViewModel.Nationality;

namespace Core.Interfaces.Services;

public interface INationalityService
{
    Task<ApiResponse<List<NationalityResponse>>> GetAllAsync(CancellationToken ct = default);
}
