using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

public partial class Permission : Entity
{
    public string Name { get; set; }

    public string Code { get; set; }

    public string Module { get; set; }

    public string Description { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

}
