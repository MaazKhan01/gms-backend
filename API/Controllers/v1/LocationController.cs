using Core.Authorization;
using Core.Common;
using Core.Interfaces.Services;
using Core.ViewModel.Location;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    public class LocationController(ILocationService _locationService) : Controllers.BaseApiController
    {
        [HttpPost]
        [HasPermission(PermissionCodes.TravelManage)]
        public async Task<IActionResult> CreateLocation([FromBody] CreateLocationDto request, CancellationToken ct)
        {
            var result = await _locationService.CreateLocationAsync(request, ct);
            return ToResponse(result);
        }
    }
}
