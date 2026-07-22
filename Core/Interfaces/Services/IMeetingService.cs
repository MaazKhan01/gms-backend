using Core.ViewModel.Common;
using Core.ViewModel.Meeting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IMeetingService
    {
        Task<ApiResponse<GetMeetingResponse>> CreateMeetingAsync(CreateMeetingRequest request, CancellationToken ct = default);
        Task<ApiResponse<List<GetMeetingResponse>>> GetMeetingAllMeetingsAsync(Guid eventId, CancellationToken ct);
        Task<ApiResponse<GetMeetingResponse>> EditMeetingAsync(EditMeetingRequest request, CancellationToken ct);
    }
}
