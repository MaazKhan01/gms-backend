using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/organizations")]
[Authorize]
[ApiVersion("1.0")]
public class OrganizationsController(IOrganizationService _organizations, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Reads are open to any authenticated user — every module needs to populate
    // an organisation dropdown. Writes stay admin-only below.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToResponse(await _organizations.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await _organizations.GetByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Organizations, AccessLevel.Write)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
        => ToResponse(await _organizations.CreateAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Organizations, AccessLevel.Write)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationRequest request, CancellationToken ct)
        => ToResponse(await _organizations.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Organizations, AccessLevel.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _organizations.DeleteAsync(id, _currentUser.UserId, ct));
}
