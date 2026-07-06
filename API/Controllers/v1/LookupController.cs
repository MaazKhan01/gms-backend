using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Lookup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/lookups")]
[Authorize]
[ApiVersion("1.0")]
public class LookupController(ILookupService _lookupService, ICurrentUser _currentUser) : Controllers.BaseApiController
{
    // Categories drive the admin submenu.
    [HttpGet("categories")]
    [HasPermission(PermissionCodes.LookupsView)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await _lookupService.GetCategoriesAsync(ct);
        return ToResponse(result);
    }

    // The core "call by code" endpoint — e.g. GET /api/v1/lookups/AIRPORT/items
    [HttpGet("{categoryCode}/items")]
    [HasPermission(PermissionCodes.LookupsView)]
    public async Task<IActionResult> GetItems(string categoryCode, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        var result = await _lookupService.GetItemsByCategoryCodeAsync(categoryCode, includeInactive, ct);
        return ToResponse(result);
    }

    [HttpGet("items/{id:guid}")]
    [HasPermission(PermissionCodes.LookupsView)]
    public async Task<IActionResult> GetItemById(Guid id, CancellationToken ct)
    {
        var result = await _lookupService.GetItemByIdAsync(id, ct);
        return ToResponse(result);
    }

    [HttpPost("items")]
    [HasPermission(PermissionCodes.LookupsManage)]
    public async Task<IActionResult> CreateItem([FromBody] LookupItemRequest request, CancellationToken ct)
    {
        var result = await _lookupService.CreateItemAsync(request, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpPut("items/{id:guid}")]
    [HasPermission(PermissionCodes.LookupsManage)]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] LookupItemRequest request, CancellationToken ct)
    {
        request.Id = id;
        var result = await _lookupService.UpdateItemAsync(request, _currentUser.UserId, ct);
        return ToResponse(result);
    }

    [HttpDelete("items/{id:guid}")]
    [HasPermission(PermissionCodes.LookupsManage)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        var result = await _lookupService.DeleteItemAsync(id, _currentUser.UserId, ct);
        return ToResponse(result);
    }
}
