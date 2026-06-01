using System;

namespace DomainPersistence.Entities;

public partial class RolePermission : Entity
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public virtual Role Role { get; set; }

    public virtual Permission Permission { get; set; }
}
