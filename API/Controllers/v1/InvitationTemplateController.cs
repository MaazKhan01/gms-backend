using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.InvitationTemplate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/invitation-templates")]
[Authorize]
[ApiVersion("1.0")]
public class InvitationTemplateController(
    IInvitationTemplateService _templateService,
    ICurrentUser _currentUser) : Controllers.BaseApiController
{
    [HttpGet]
    [HasPermission(PermissionCodes.InvitationsView)]
    public async Task<IActionResult> GetByEvent([FromQuery] Guid eventId, CancellationToken ct)
        => ToResponse(await _templateService.GetByEventAsync(eventId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.InvitationsManageTemplates)]
    public async Task<IActionResult> Create([FromBody] CreateInvitationTemplateRequest request, CancellationToken ct)
        => ToResponse(await _templateService.CreateAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.InvitationsManageTemplates)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvitationTemplateRequest request, CancellationToken ct)
        => ToResponse(await _templateService.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.InvitationsManageTemplates)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _templateService.DeleteAsync(id, _currentUser.UserId, ct));
}
