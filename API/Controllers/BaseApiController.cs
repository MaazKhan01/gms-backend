using Microsoft.AspNetCore.Mvc;
using Core.ViewModel.Common;

namespace API.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult ToResponse<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
