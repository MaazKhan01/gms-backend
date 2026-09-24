using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Department;

namespace API.Controllers.v1;

/// <summary>Departments — admin-managed reference data.</summary>
[Route("api/v1/departments")]
[Authorize]
[ApiVersion("1.0")]
public class DepartmentsController(IDepartmentService _departments, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    // Open to any signed-in user: the department dropdown appears on the delegate
    // form and on Nominations. Every write below is gated.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToResponse(await _departments.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await _departments.GetByIdAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Lookups, AccessLevel.Write)]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request, CancellationToken ct)
        => ToResponse(await _departments.CreateAsync(request, _currentUser.UserId, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Lookups, AccessLevel.Write)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken ct)
        => ToResponse(await _departments.UpdateAsync(id, request, _currentUser.UserId, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Lookups, AccessLevel.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await _departments.DeleteAsync(id, _currentUser.UserId, ct));
}
