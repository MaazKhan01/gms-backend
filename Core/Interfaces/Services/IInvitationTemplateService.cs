using Core.ViewModel.Common;
using Core.ViewModel.InvitationTemplate;

namespace Core.Interfaces.Services;

public interface IInvitationTemplateService
{
    Task<ApiResponse<List<InvitationTemplateResponse>>> GetByEventAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResponse<InvitationTemplateResponse>> CreateAsync(CreateInvitationTemplateRequest request, Guid createdBy, CancellationToken ct = default);
    Task<ApiResponse<InvitationTemplateResponse>> UpdateAsync(Guid id, UpdateInvitationTemplateRequest request, Guid updatedBy, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
}
