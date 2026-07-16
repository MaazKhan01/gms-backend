using Core.Interfaces.Services;
using Core.ViewModel.Meeting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class MeetingController(IMeetingService _meetingService) : Controllers.BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest request, CancellationToken ct)
    {
        var result = await _meetingService.CreateMeetingAsync(request, ct);
        return ToResponse(result);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMeetingAllMeetings(Guid id, CancellationToken ct)
    {
        var result =await _meetingService.GetMeetingAllMeetingsAsync(id, ct);
        return ToResponse(result);
    }
 }