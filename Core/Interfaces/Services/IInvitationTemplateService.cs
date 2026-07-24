using Core.ViewModel.Common;
using Core.ViewModel.InvitationTemplate;

namespace Core.Interfaces.Services;

public interface IInvitationTemplateService
{
    Task<ApiResponse<List<InvitationTemplateResponse>>> GetByEventAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<InvitationTemplateResponse>> CreateAsync(CreateInvitationTemplateRequest request, int createdBy, CancellationToken ct = default);
    Task<ApiResponse<InvitationTemplateResponse>> UpdateAsync(Guid id, UpdateInvitationTemplateRequest request, int updatedBy, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int deletedBy, CancellationToken ct = default);
}
