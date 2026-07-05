using AutoMapper;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Nationality;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class NationalityService(IUnitOfWork _unitOfWork, IMapper _mapper, ILogger<NationalityService> _logger) : INationalityService
{
    public async Task<ApiResponse<List<NationalityResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await _unitOfWork.Nationalities.Query()
                .OrderBy(n => n.Name)
                .ToListAsync(ct);

            return ApiResponse<List<NationalityResponse>>.SuccessResponse(_mapper.Map<List<NationalityResponse>>(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nationalities");
            return ApiResponse<List<NationalityResponse>>.ServerErrorResponse("An error occurred while retrieving nationalities");
        }
    }
}
