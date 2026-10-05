using Core.Authorization;
using Core.Common;
using Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    public class DashboardController(IDashboardService _dashboardService) : Controllers.BaseApiController
    {
        [HttpGet("{eventId:guid}")]
        [HasPermission(PermissionCodes.Events)]
        public async Task<IActionResult> GetDashboard(Guid eventId, CancellationToken ct)
        {
            var result = await _dashboardService.GetDashboardAsync(eventId, ct);
            return ToResponse(result);
        }
    }
}
