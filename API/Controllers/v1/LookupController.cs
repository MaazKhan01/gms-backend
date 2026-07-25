using Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/lookups")]
[Authorize]
[ApiVersion("1.0")]
public class LookupController(ILookupService _lookupService) : Controllers.BaseApiController
{
    // Code-defined guest option sets (tier, type, statuses) for form dropdowns.
    // Any authenticated user can read these — they're static reference lists.
    [HttpGet("enums/guest")]
    public IActionResult GetGuestEnums()
        => ToResponse(_lookupService.GetGuestEnums());
}
