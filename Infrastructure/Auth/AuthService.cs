using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Core.Constants;
using Core.Interfaces;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Auth;
using Core.ViewModel.Common;
using Core.ViewModel.User;
using DomainPersistence.Entities;
using Infrastructure.Services;

namespace Infrastructure.Auth;

public class AuthService(
    IUnitOfWork _unitOfWork,
    IConfiguration _configuration,
    ILogger<AuthService> _logger,
    IEmailService _emailService,
    IPasswordResetTokenService _passwordResetTokenService,
    IMapper _mapper) : IAuthService
{
    public async Task<ApiResponse<TokenResponse>> LoginAsync(LoginModel model, string clientApp = null, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Include(u => u.ModuleGrants)
            .Include(u => u.DriverProfile)
                .ThenInclude(d => d.Nationality)
            .FirstOrDefaultAsync(u => u.Email == model.Email && u.IsDeleted != true, ct);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid credentials");

        if (!user.IsActive)
            return ApiResponse<TokenResponse>.UnauthorizedResponse("Account is inactive");

        // Portal sign-in is a per-role privilege (Roles.PortalAccess). Driver-only
        // roles have it off, so they can only sign in from the driver app.
        var client = ClientApps.Normalize(clientApp);
        if (client == ClientApps.Portal && user.Role?.PortalAccess != true)
            return ApiResponse<TokenResponse>.ForbiddenResponse("This account cannot sign in to the portal");

        var (accessToken, refreshToken, jti) = GenerateTokenPair(user, client);

        // Store refresh token JTI in DB for revocation support
        await _unitOfWork.UserRefreshTokens.AddAsync(new UserRefreshToken
        {
            UserId = user.Id,
            Jti = jti,
            ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays()),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        var response = BuildTokenResponse(user, accessToken, refreshToken);
        // A client that sent a device token registers it here; everyone else
        // just gets their most recent registration echoed back.
        var device = string.IsNullOrWhiteSpace(model.DeviceToken)
            ? await GetLatestDeviceAsync(user.Id, ct)
            : await UpsertDeviceAsync(user.Id, model, ct);
        response.FcmToken = device?.Token;
        response.DeviceId = device?.DeviceIdentifier;

        return ApiResponse<TokenResponse>.SuccessResponse(response, "Login successful");
    }

    private async Task<Device> UpsertDeviceAsync(int userId, LoginModel model, CancellationToken ct)
    {
        try
        {
            return await DeviceRegistration.UpsertAsync(
                _unitOfWork, userId, model.DeviceToken,
                model.Platform, model.DeviceIdentifier, model.DeviceModel, ct: ct);
        }
        catch (Exception ex)
        {
            // Never fail a valid login over push registration.
            _logger.LogError(ex, "Device registration during login failed for user {UserId}", userId);
            return null;
        }
    }

    // Read-only fallback — reports the caller's most recently active
    // registration when login carried no device token.
    private Task<Device> GetLatestDeviceAsync(int userId, CancellationToken ct)
        => _unitOfWork.Devices.Query()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.LastActiveAt)
            .FirstOrDefaultAsync(ct);

    public async Task<ApiResponse<TokenResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var principal = ValidateJwt(refreshToken, validateLifetime: true);
            var email = principal.FindFirstValue(ClaimTypes.Email);
            var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(jti))
                return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid refresh token");

            // An access token is signed with the same key, so without this an
            // access token posted here would be treated as a refresh token.
            if (principal.FindFirstValue("token_type") != "refresh")
                return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid refresh token");

            // Validate JTI is in DB and not revoked
            var storedToken = await _unitOfWork.UserRefreshTokens
                .Query()
                .FirstOrDefaultAsync(t => t.Jti == jti && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow, ct);

            if (storedToken == null)
                return ApiResponse<TokenResponse>.UnauthorizedResponse("Refresh token has been revoked or expired");

            var user = await _unitOfWork.Users
                .Query()
                .Include(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .Include(u => u.ModuleGrants)
                .Include(u => u.DriverProfile)
                    .ThenInclude(d => d.Nationality)
                .FirstOrDefaultAsync(u => u.Email == email && u.IsDeleted != true, ct);

            if (user == null || !user.IsActive)
                return ApiResponse<TokenResponse>.UnauthorizedResponse("User not found or inactive");

            // Same client as the session being refreshed, and the portal privilege
            // is re-checked — revoking a role's portal access kills its sessions at
            // the next refresh instead of lingering for the token's lifetime.
            var client = ClientApps.Normalize(principal.FindFirstValue("client"));
            if (client == ClientApps.Portal && user.Role?.PortalAccess != true)
                return ApiResponse<TokenResponse>.ForbiddenResponse("This account cannot sign in to the portal");

            // No rotation. The refresh token is a stable handle that stays valid for
            // its whole RefreshTokenExpirationDays window; refreshing only mints a
            // new access token. Rotating used to revoke the token the caller still
            // held, so a second tab (or a retry after a dropped response) refreshed
            // with an already-revoked token and got logged out.
            // ponytail: no rotation also means no stolen-token reuse detection —
            // revocation is logout + the UserRefreshTokens row. Bring rotation back
            // only together with client-side single-flight across tabs.
            var newAccess = GenerateAccessToken(user, client);

            return ApiResponse<TokenResponse>.SuccessResponse(
                BuildTokenResponse(user, newAccess, refreshToken), "Token refreshed successfully");
        }
        catch (SecurityTokenException ex)
        {
            return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid refresh token", new List<string> { ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh token failed");
            return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid refresh token");
        }
    }

    public async Task<ApiResponse<bool>> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var principal = ValidateJwt(refreshToken, validateLifetime: false);
            var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);

            // Guest sessions share this table with a "guest-refresh" token_type, so
            // without this check a guest token posted here would revoke its row via
            // the staff endpoint. Access tokens are turned away for the same reason.
            if (string.IsNullOrEmpty(jti) || principal.FindFirstValue("token_type") != "refresh")
                return ApiResponse<bool>.SuccessResponse(true, "Logged out");

            var storedToken = await _unitOfWork.UserRefreshTokens
                .Query()
                .FirstOrDefaultAsync(t => t.Jti == jti && !t.IsRevoked, ct);

            if (storedToken != null)
            {
                storedToken.IsRevoked = true;
                _unitOfWork.UserRefreshTokens.Update(storedToken);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return ApiResponse<bool>.SuccessResponse(true, "Logged out successfully");
        }
        catch
        {
            // Silent — logout should always succeed from user perspective
            return ApiResponse<bool>.SuccessResponse(true, "Logged out successfully");
        }
    }

    public async Task<ApiResponse<TokenResponse>> ValidateToken(string token, CancellationToken ct = default)
    {
        try
        {
            var jwtSection = _configuration.GetSection("Authentication:Jwt");
            var principal = ValidateJwt(token, validateLifetime: true,
                issuer: jwtSection["Issuer"], audience: jwtSection["Audience"]);

            var email = principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
                return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid token: email claim missing");

            var user = await _unitOfWork.Users
                .Query()
                .Include(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .Include(u => u.DriverProfile)
                    .ThenInclude(d => d.Nationality)
                .FirstOrDefaultAsync(u => u.Email == email && u.IsDeleted != true, ct);

            if (user == null || !user.IsActive)
                return ApiResponse<TokenResponse>.UnauthorizedResponse("User not found or inactive");

            var response = BuildTokenResponse(user, token, null);
            return ApiResponse<TokenResponse>.SuccessResponse(response, "Token is valid");
        }
        catch (SecurityTokenException ex)
        {
            return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid token", new List<string> { ex.Message });
        }
    }

    public async Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsDeleted != true, ct);

            // Always return success to prevent email enumeration
            if (user == null || !user.IsActive)
                return ApiResponse<bool>.SuccessResponse(true, "If an account with that email exists, a reset link has been sent.");

            var resetToken = await _passwordResetTokenService.GenerateToken(user.PublicId, user.Email);
            var frontendUrl = _configuration["FrontendUrl"];
            var resetLink = $"{frontendUrl}/reset-password?token={resetToken}";

            await _emailService.SendResetPasswordLinkAsync(user.Email, resetLink, ct);
            return ApiResponse<bool>.SuccessResponse(true, "If an account with that email exists, a reset link has been sent.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot password failed for {Email}", request.Email);
            return ApiResponse<bool>.ServerErrorResponse("Failed to process request");
        }
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            var userId = await _passwordResetTokenService.ValidateToken(request.Token);
            var user = await _unitOfWork.Users.GetByPublicIdAsync(userId, ct);

            if (user == null)
                return ApiResponse<bool>.NotFoundResponse("Invalid token");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Password reset successful");
        }
        catch (SecurityTokenException)
        {
            return ApiResponse<bool>.UnauthorizedResponse("Token is invalid or expired");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset password failed");
            return ApiResponse<bool>.ServerErrorResponse("Failed to reset password");
        }
    }

    public async Task<ApiResponse<UserResponse>> ValidateResetPasswordTokenAsync(ValidateToken token, CancellationToken ct = default)
    {
        try
        {
            var userId = await _passwordResetTokenService.ValidateToken(token.Token);
            var user = await _unitOfWork.Users.GetByPublicIdAsync(userId, ct);

            if (user == null)
                return ApiResponse<UserResponse>.NotFoundResponse("Invalid token");

            return ApiResponse<UserResponse>.SuccessResponse(_mapper.Map<UserResponse>(user), "Token is valid");
        }
        catch (SecurityTokenException)
        {
            return ApiResponse<UserResponse>.UnauthorizedResponse("Token is invalid or expired");
        }
    }

    public async Task<ApiResponse<TokenResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default)
    {
        try
        {
            var otp = await _unitOfWork.OtpVerifications.Query()
                .Where(o => o.Email == request.Email && o.OtpCode == request.OtpCode
                         && o.Purpose == "email-verification" && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (otp == null)
                return ApiResponse<TokenResponse>.UnauthorizedResponse("Invalid verification code");

            if (otp.ExpiresAt < DateTime.UtcNow)
                return ApiResponse<TokenResponse>.UnauthorizedResponse("Verification code has expired. Please request a new one.");

            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;

            var user = await _unitOfWork.Users
                .Query()
                .Include(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .Include(u => u.ModuleGrants)
                .Include(u => u.DriverProfile)
                    .ThenInclude(d => d.Nationality)
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<TokenResponse>.NotFoundResponse("User not found");

            var (accessToken, refreshToken, jti) = GenerateTokenPair(user);

            await _unitOfWork.UserRefreshTokens.AddAsync(new UserRefreshToken
            {
                UserId = user.Id,
                Jti = jti,
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays()),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<TokenResponse>.SuccessResponse(BuildTokenResponse(user, accessToken, refreshToken), "Email verified successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OTP verification failed for {Email}", request.Email);
            return ApiResponse<TokenResponse>.ServerErrorResponse("Verification failed");
        }
    }

    public async Task<ApiResponse<bool>> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<bool>.SuccessResponse(true, "If an account with that email exists, a new OTP has been sent.");

            var existingOtps = await _unitOfWork.OtpVerifications.Query()
                .Where(o => o.Email == request.Email && o.Purpose == "email-verification" && !o.IsUsed)
                .ToListAsync(ct);

            foreach (var existing in existingOtps)
            {
                existing.IsUsed = true;
                existing.UsedAt = DateTime.UtcNow;
            }

            var otpCode = GenerateOtpCode();
            await _unitOfWork.OtpVerifications.AddAsync(new OtpVerification
            {
                Email = request.Email,
                OtpCode = otpCode,
                Purpose = "email-verification",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _emailService.SendOtpEmailAsync(request.Email, otpCode, ct);
            return ApiResponse<bool>.SuccessResponse(true, "If an account with that email exists, a new OTP has been sent.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend OTP failed for {Email}", request.Email);
            return ApiResponse<bool>.ServerErrorResponse("Failed to resend verification code");
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    // 4 digits, 1000-9999 so it never renders with a leading zero.
    private static string GenerateOtpCode() => Random.Shared.Next(1000, 10000).ToString();

    private int GetRefreshTokenExpiryDays()
        => int.Parse(_configuration.GetSection("Authentication:Jwt")["RefreshTokenExpirationDays"] ?? "30");

    // clientApp is stamped into the access token as the "client" claim, so a
    // token minted for one client can be recognised (and rejected) by the other.
    private (string accessToken, string refreshToken, string jti) GenerateTokenPair(User user, string clientApp = ClientApps.Portal)
    {
        var accessToken = GenerateAccessToken(user, clientApp);
        var jti = Guid.NewGuid().ToString();
        var refreshToken = GenerateRefreshToken(user.Id, user.Email, jti, clientApp);
        return (accessToken, refreshToken, jti);
    }

    private string GenerateAccessToken(User user, string clientApp = ClientApps.Portal)
    {
        var jwtSection = _configuration.GetSection("Authentication:Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["JwtSecretKey"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        var claims = new List<Claim>
        {
            // Standard claims (used by server-side auth handlers)
            new("sub", user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, fullName),
            new("role", user.Role?.Name ?? string.Empty),

            // Short-named claims so the frontend can decode everything it needs
            // straight from the access token (no separate /me call or localStorage).
            new("uid", user.Id.ToString()),
            new("email", user.Email ?? string.Empty),
            new("userName", user.UserName ?? string.Empty),
            new("firstName", user.FirstName ?? string.Empty),
            new("lastName", user.LastName ?? string.Empty),
            new("fullName", fullName),
            new("roleCode", user.Role?.Code ?? string.Empty),
            new("roleId", user.RoleId?.ToString() ?? string.Empty),
            new("client", ClientApps.Normalize(clientApp)),
        };
        // No Guest.Id claim here (or on the guest token either) — ICurrentGuest
        // resolves Guest.Id from Guests.UserId, gated on role=="guest".

        // Role-based permissions — gate both server [HasPermission] and frontend nav.
        var addedPerms = new HashSet<string>();
        if (user.Role?.RolePermissions != null)
            foreach (var rp in user.Role.RolePermissions.Where(rp => rp.Permission != null))
                if (addedPerms.Add(rp.Permission.Code))
                    claims.Add(new Claim("permission", rp.Permission.Code));

        // Admin-granted extra module read access (cross-module view only).
        if (user.ModuleGrants != null)
            foreach (var grant in user.ModuleGrants.Where(g => g.IsGranted))
                if (ModuleDefinitions.ViewPermissionBySlug.TryGetValue(grant.Module, out var perm)
                    && addedPerms.Add(perm))
                    claims.Add(new Claim("permission", perm));

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtSection["ExpirationMinutes"] ?? "60")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Carries the client too, so a refresh re-issues an access token for the same
    // client instead of silently upgrading a driver-app session to a portal one.
    private string GenerateRefreshToken(int userId, string email, string jti, string clientApp = ClientApps.Portal)
    {
        var jwtSection = _configuration.GetSection("Authentication:Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["JwtSecretKey"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(ClaimTypes.Email, email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("token_type", "refresh"),
            new("client", ClientApps.Normalize(clientApp))
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays()),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateJwt(string token, bool validateLifetime, string issuer = null, string audience = null)
    {
        var jwtSection = _configuration.GetSection("Authentication:Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["JwtSecretKey"]));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = validateLifetime,
            ValidateIssuer = !string.IsNullOrEmpty(issuer),
            ValidIssuer = issuer,
            ValidateAudience = !string.IsNullOrEmpty(audience),
            ValidAudience = audience,
            ClockSkew = TimeSpan.Zero
        };

        return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
    }

    private static TokenResponse BuildTokenResponse(User user, string accessToken, string refreshToken)
    {
        var isDriver = user.Role?.Code == Roles.DRIVER;

        return new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserInfo
            {
                Id = user.PublicId,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role?.Name,
                RoleCode = user.Role?.Code,
                RoleId = user.Role?.PublicId,
                // Drivers get driverDetails instead; both props are omitted when null.
                Permissions = isDriver ? null : user.Role?.RolePermissions?
                    .Where(rp => rp.Permission != null)
                    .Select(rp => rp.Permission.Code)
                    .ToList() ?? new(),
                DriverDetails = isDriver && user.DriverProfile != null
                    ? new DriverDetails
                    {
                        Id = user.DriverProfile.PublicId,
                        DriverType = user.DriverProfile.DriverType?.ToString(),
                        LicenseNumber = user.DriverProfile.LicenseNumber,
                        LicenseExpiry = user.DriverProfile.LicenseExpiry,
                        NationalityId = user.DriverProfile.Nationality?.PublicId,
                        Nationality = user.DriverProfile.Nationality?.Name,
                        PhotoUrl = user.DriverProfile.PhotoUrl
                    }
                    : null
            }
        };
    }
}
