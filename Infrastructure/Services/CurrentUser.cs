using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Common;
using Core.Common.Interfaces;
using Core.Interfaces.Repositories;

namespace Infrastructure.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CurrentUser> _logger;
    private LoggedInUser _userInfo;
    private bool _isInitialized;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork, ILogger<CurrentUser> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public LoggedInUser UserInfo
    {
        get
        {
            if (!_isInitialized)
            {
                _userInfo = InitializeUserInfo();
                _isInitialized = true;
            }
            return _userInfo;
        }
    }

    public int UserId => UserInfo?.Id ?? 0;
    public Guid UserPublicId => UserInfo?.PublicId ?? Guid.Empty;
    public string Email => UserInfo?.Email;
    public int RoleId => UserInfo?.RoleId ?? 0;
    public string RoleName => UserInfo?.RoleName;
    public bool IsAuthenticated => UserInfo != null && UserId != 0;

    private LoggedInUser InitializeUserInfo()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (!int.TryParse(userIdClaim, out var userId) || userId == 0)
            {
                _logger.LogWarning("User ID claim not found or invalid in token");
                return null;
            }

            var userEntity = _unitOfWork.Users
                .Query()
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefault(x => x.Id == userId && x.IsDeleted != true);

            if (userEntity == null)
            {
                _logger.LogWarning("User not found in database for ID: {UserId}", userId);
                return null;
            }

            return new LoggedInUser
            {
                Id = userEntity.Id,
                PublicId = userEntity.PublicId,
                FirstName = userEntity.FirstName,
                LastName = userEntity.LastName,
                Email = userEntity.Email,
                Phone = userEntity.Phone,
                RoleId = userEntity.RoleId ?? 0,
                RoleName = userEntity.Role?.Name,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing user info");
            return null;
        }
    }

    public void OverrideCurrentSession(LoggedInUser user)
    {
        _userInfo = user;
        _isInitialized = true;
        _logger.LogInformation("User session overridden for: {Email}", user?.Email);
    }

    public void RequireRole(string roleName)
    {
        if (!HasRole(roleName))
            throw new UnauthorizedAccessException($"Access denied. Required role: {roleName}");
    }

    public bool HasRole(string roleName)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(roleName))
            return false;
        return string.Equals(RoleName, roleName, StringComparison.OrdinalIgnoreCase);
    }

    public void RequireAuthentication()
    {
        if (!IsAuthenticated)
            throw new UnauthorizedAccessException("Access denied. Authentication required.");
    }

    // Same claim type PermissionAuthorizationHandler reads, so an in-service check
    // and an endpoint's [HasPermission] can never disagree. Note this reads the
    // TOKEN, not the DB: a permission granted after the token was issued only
    // takes effect on refresh — identical to how every [HasPermission] behaves.
    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return false;

        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return false;

        return user.FindAll("permission").Any(c => c.Value == permissionCode);
    }
}
