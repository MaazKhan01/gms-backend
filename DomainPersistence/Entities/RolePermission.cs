using System;

namespace DomainPersistence.Entities;

/// <summary>
/// One role's access to one menu/submenu. Read and Write are independent —
/// holding Read says nothing about Write. No row, or a row with both flags
/// false, means no access at all.
/// </summary>
public partial class RolePermission : Entity
{
    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    /// <summary>May open the page and read its data.</summary>
    public bool CanRead { get; set; }

    /// <summary>May create / update / delete through it.</summary>
    public bool CanWrite { get; set; }

    public virtual Role Role { get; set; }

    public virtual Permission Permission { get; set; }
}
