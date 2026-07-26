using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Core.Common.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.AccountRequest;
using Core.ViewModel.Auth;
using Core.ViewModel.Common;
using DomainPersistence.Entities;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
public class AuthController : Controllers.BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;

    public AuthController(IUnitOfWork unitOfWork, IAuthService authService, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginModel model, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(model, ct);

        if (result.Success)
        {
            // result.Data.User.Id is the public Guid; resolve to the internal int for the FK.
            var loggedUser = await _unitOfWork.Users.GetByPublicIdAsync(result.Data.User.Id, ct);
            await _unitOfWork.UserLoginLogs.AddAsync(new UserLoginLog
            {
                UserId = loggedUser?.Id ?? 0,
                LoginAt = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                IsSuccessful = true
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return ToResponse(result);
    }

    // Public self-registration is disabled — only an admin can add a user
    // (see UsersController.InviteUser). Kept the AccountRequest infrastructure
    // in place since it's still used for the approve/reject audit trail.

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest model, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(model.RefreshToken, ct);
        return ToResponse(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        // Fix 6: Revoke refresh token so it can no longer be used
        if (!string.IsNullOrEmpty(request?.RefreshToken))
            await _authService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Logged out successfully"));
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        var result = await _authService.VerifyOtpAsync(request, ct);
        return ToResponse(result);
    }

    [HttpPost("resend-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken ct)
    {
        var result = await _authService.ResendOtpAsync(request, ct);
        return ToResponse(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ForgotPasswordAsync(request, ct);
        return ToResponse(result);
    }

    [HttpPost("validate-reset-password-token")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateResetPasswordToken([FromBody] ValidateToken token, CancellationToken ct)
    {
        var result = await _authService.ValidateResetPasswordTokenAsync(token, ct);
        return ToResponse(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ResetPasswordAsync(request, ct);
        return ToResponse(result);
    }

    [HttpGet("validate-token/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateLoginToken(string token, CancellationToken ct)
    {
        var result = await _authService.ValidateToken(token, ct);
        return ToResponse(result);
    }
}
