using System;
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

    public Guid UserId => UserInfo?.Id ?? Guid.Empty;
    public string Email => UserInfo?.Email;
    public Guid RoleId => UserInfo?.RoleId ?? Guid.Empty;
    public string RoleName => UserInfo?.RoleName;
    public bool IsAuthenticated => UserInfo != null && UserId != Guid.Empty;

    private LoggedInUser InitializeUserInfo()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
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
                FirstName = userEntity.FirstName,
                LastName = userEntity.LastName,
                Email = userEntity.Email,
                Phone = userEntity.Phone,
                RoleId = userEntity.RoleId ?? Guid.Empty,
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
}
