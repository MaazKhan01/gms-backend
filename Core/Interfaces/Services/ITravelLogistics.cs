using Core.ViewModel.Common;
using Core.ViewModel.Travel_Logistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface ITravelLogistics
    {
        Task<ApiResponse<GetBookingResponse>> CreateBookingAsync(CreateBookingDto request, Guid userId, CancellationToken ct);
    }
}
