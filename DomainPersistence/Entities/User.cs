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

    public virtual Role? Role { get; set; }
    public virtual ICollection<UserModuleGrant> ModuleGrants { get; set; } = new List<UserModuleGrant>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<UserLoginLog> UserLoginLogs { get; set; } = new List<UserLoginLog>();
    public virtual ICollection<SystemErrorLog> SystemErrorLogs { get; set; } = new List<SystemErrorLog>();
}
