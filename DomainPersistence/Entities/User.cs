using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

public partial class User : Entity
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public int? RoleId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public string? PasswordHash { get; set; }

    // Set when an admin invites this user (no password yet, IsActive false).
    // Cleared once they accept the invite and set their own password.
    public Guid? InviteToken { get; set; }
    public DateTime? InviteSentAt { get; set; }

    public virtual Role? Role { get; set; }
    public virtual DriverProfile? DriverProfile { get; set; }
    // Present only for Users backed by a Guest (RoleId -> "guest"). Null for
    // every other User — see Guest.UserId for the owning side of this 1:1.
    public virtual Guest? GuestProfile { get; set; }
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
    public virtual ICollection<UserLoginLog> UserLoginLogs { get; set; } = new List<UserLoginLog>();
    public virtual ICollection<SystemErrorLog> SystemErrorLogs { get; set; } = new List<SystemErrorLog>();
}
