using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Interfaces.Services;
using Core.ViewModel.Common;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[Authorize]
[ApiVersion("1.0")]
public class UploadController : Controllers.BaseApiController
{
    private readonly IBlobService _blobService;

    public UploadController(IBlobService blobService)
    {
        _blobService = blobService;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage([FromBody] UploadRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Base64Image))
            return BadRequest(ApiResponse<object>.ErrorResponse("base64Image is required"));

        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(ApiResponse<object>.ErrorResponse("fileName is required"));

        var blobUrl = await _blobService.UploadBase64Async(request.Base64Image, request.FileName, ct: ct);
        var sasUrl = _blobService.GenerateSasUrl(blobUrl);

        return Ok(ApiResponse<object>.SuccessResponse(new { imageUrl = sasUrl }, "Image uploaded successfully"));
    }
}

public class UploadRequest
{
    public string Base64Image { get; set; }
    public string FileName { get; set; }
}
