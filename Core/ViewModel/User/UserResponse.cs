using System;
using System.Collections.Generic;

namespace Core.ViewModel.User;

public class UserResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Role { get; set; }
    public string RoleName { get; set; }
    /// <summary>The role's public id — what the edit form posts back.</summary>
    public Guid? RoleId { get; set; }
    public bool IsActive { get; set; }
    public List<string> Permissions { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    // Only meaningful right after InviteUserAsync — true elsewhere/by default,
    // since most callers of this DTO don't just send an invite.
    public bool InviteEmailSent { get; set; } = true;
    // Why the send failed, when it did — the account exists either way, so the
    // admin needs the provider's reason to know whether Resend Invite will help.
    // Null when the email went out. Admin-only endpoint (Users.Create).
    public string InviteEmailError { get; set; }
}
