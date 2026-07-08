using Core.ViewModel.Common;
using Core.ViewModel.GuestTravelLogistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IGuestBookingService 
    {
        Task<ApiResponse<GuestBookingResponse>> CreateGuestBookingAsync(GuestBookingRequest request , CancellationToken ct);
    }
}
