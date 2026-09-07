using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Core.Common.Interfaces;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.AccountRequest;
using Core.ViewModel.Auth;
using Core.ViewModel.Common;
using Core.ViewModel.User;
using DomainPersistence.Entities;

namespace API.Controllers.v1;

[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
public class AuthController : Controllers.BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;
    private readonly IUserService _userService;

    public AuthController(IUnitOfWork unitOfWork, IAuthService authService, ICurrentUser currentUser, IUserService userService)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _currentUser = currentUser;
        _userService = userService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    /// <param name="clientApp">"portal" (default) or "driver-app". Declared as a
    /// parameter so Swagger offers a box for it; real clients send the header.</param>
    public async Task<IActionResult> Login(
        [FromBody] LoginModel model,
        [FromHeader(Name = ClientApps.Header)] string clientApp,
        CancellationToken ct)
    {
        var result = await _authService.LoginAsync(model, clientApp, ct);

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
    [EnableRateLimiting("auth-refresh")]
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

    // Open (unauthenticated) user creation: email + full name + role name.
    // Password is optional — leave it out and the user gets in via
    // forgot-password or POST /v1/users/{id}/admin-set-password.
    // [HttpPost("create-user")]
    // [AllowAnonymous]
    // [EnableRateLimiting("auth")]
    // public async Task<IActionResult> CreateUser([FromBody] QuickCreateUserRequest request, CancellationToken ct)
    // {
    //     if (string.IsNullOrWhiteSpace(request?.Email) ||
    //         string.IsNullOrWhiteSpace(request.Name) ||
    //         string.IsNullOrWhiteSpace(request.Role))
    //         return ToResponse(ApiResponse<UserResponse>.ErrorResponse("Email, name and role are all required"));

    //     var roleKey = request.Role.Trim().ToLower();
    //     var role = await _unitOfWork.Roles.FindFirstOrDefaultAsync(
    //         r => r.Code.ToLower() == roleKey || r.Name.ToLower() == roleKey, ct);
    //     if (role == null)
    //         return ToResponse(ApiResponse<UserResponse>.NotFoundResponse($"Role '{request.Role}' not found"));

    //     // ponytail: first token is the first name, the rest is the last name.
    //     var parts = request.Name.Trim().Split(' ', 2);

    //     var result = await _userService.CreateUserAsync(new CreateUserRequest
    //     {
    //         FirstName = parts[0],
    //         LastName = parts.Length > 1 ? parts[1].Trim() : string.Empty,
    //         Email = request.Email,
    //         Password = request.Password,
    //         RoleId = role.PublicId
    //     }, ct);

    //     return ToResponse(result);
    // }
}
