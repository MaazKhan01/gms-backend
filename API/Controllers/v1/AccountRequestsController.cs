using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.AccountRequest;
using Core.ViewModel.Common;

namespace API.Controllers.v1;

[Route("api/v1/account-requests")]
[Authorize]
[ApiVersion("1.0")]
public class AccountRequestsController(IAccountRequestService _service, ICurrentUser _currentUser)
    : Controllers.BaseApiController
{
    [HttpGet]
    [HasPermission(PermissionCodes.AccountRequests)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string search = null,
        [FromQuery] string status = "pending",
        CancellationToken ct = default)
    {
        var result = await _service.GetRequestsAsync(
            new PagedRequest { PageNumber = pageNumber, PageSize = pageSize, SearchTerm = search }, status, ct);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/approve")]
    [HasPermission(PermissionCodes.AccountRequests, AccessLevel.Write)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveAccountRequest decision, CancellationToken ct)
        => ToResponse(await _service.ApproveAsync(id, decision, _currentUser.UserId, ct));

    [HttpPost("{id:guid}/reject")]
    [HasPermission(PermissionCodes.AccountRequests, AccessLevel.Write)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectAccountRequest decision, CancellationToken ct)
        => ToResponse(await _service.RejectAsync(id, decision, _currentUser.UserId, ct));
}
