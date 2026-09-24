using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

public partial class Role : Entity
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }

    // Can this role sign in to the admin portal? Driver-only roles get false —
    // they may only sign in from the driver app.
    public bool PortalAccess { get; set; }
    public bool IsDelegateRole { get; set; }
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
