using Core.Authorization;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Services;
using Core.ViewModel.Travel_Logistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiVersion("1.0")]
    public class TravelLogisticsController(ITravelLogistics _travelLogistics, ICurrentUser _currentUser) : Controllers.BaseApiController
    {
        [HttpPost]
        [HasPermission(PermissionCodes.TravelManage)]
        public async Task<IActionResult> CreateGuestBooking([FromBody] CreateBookingDto request, CancellationToken ct)
        {
            var result = await _travelLogistics.CreateBookingAsync(request, _currentUser.UserId, ct);
            return ToResponse(result);
        }
    }
}
