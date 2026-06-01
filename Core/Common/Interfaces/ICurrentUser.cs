using System;

namespace Core.Common.Interfaces;

public interface ICurrentUser
{
    LoggedInUser UserInfo { get; }
    Guid UserId { get; }
    string Email { get; }
    Guid RoleId { get; }
    string RoleName { get; }
    bool IsAuthenticated { get; }

    // Override current session with explicit user object (useful for background jobs / impersonation)
    void OverrideCurrentSession(LoggedInUser user);

    void RequireRole(string roleName);
    bool HasRole(string roleName);
    void RequireAuthentication();
}
