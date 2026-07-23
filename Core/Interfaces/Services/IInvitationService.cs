using Core.ViewModel.Common;
using Core.ViewModel.Invitation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IInvitationService
    {
        Task<ApiResponse<InvitationDetailResponse>> GetByTokenAsync(Guid token, CancellationToken ct);
        Task<ApiResponse<InvitationDetailResponse>> RespondAsync(Guid token, RespondToInvitationRequest request, CancellationToken ct);
    }
}
