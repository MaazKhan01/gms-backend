using System;

namespace Core.Common.Interfaces;

public interface ICurrentUser
{
    LoggedInUser UserInfo { get; }
    int UserId { get; }
    Guid UserPublicId { get; }
    string Email { get; }
    int RoleId { get; }
    string RoleName { get; }
    bool IsAuthenticated { get; }

    // Override current session with explicit user object (useful for background jobs / impersonation)
    void OverrideCurrentSession(LoggedInUser user);

    void RequireRole(string roleName);
    bool HasRole(string roleName);
    void RequireAuthentication();

    /// <summary>True when the caller's token carries this permission code. Reads
    /// the same claims the <c>[HasPermission]</c> policy handler checks, so
    /// attribute-gated and in-service decisions can never disagree.</summary>
    bool CanRead(string permissionCode);

    /// <summary>True when the caller's role may WRITE this permission. Holding
    /// read grants nothing here. For rules that live INSIDE a request body (e.g. a
    /// guest save that also asks to waive a Service Level rule) rather than on
    /// the endpoint itself.</summary>
    bool CanWrite(string permissionCode);
}
