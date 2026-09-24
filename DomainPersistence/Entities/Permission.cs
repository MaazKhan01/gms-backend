using System;
using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>
/// One navigable thing in the application: a menu, or a submenu of one. This is
/// the single source of truth for what the portal can show — the frontend builds
/// its navigation from these rows and hardcodes nothing.
///
/// Self-referencing: <see cref="ParentId"/> null means a top-level menu,
/// otherwise the row is a submenu of that parent.
/// </summary>
public partial class Permission : Entity
{
    /// <summary>
    /// Stable machine identifier. The frontend maps it to a React component and
    /// the backend to an authorization policy
    /// (<c>[HasPermission(PermissionCodes.Guests)]</c>), so it must never change
    /// once referenced. Lower-kebab by convention.
    /// </summary>
    public string Code { get; set; }

    public string Name { get; set; }

    /// <summary>Arabic label. Falls back to <see cref="Name"/> when null.</summary>
    public string NameAr { get; set; }

    public string Description { get; set; }

    /// <summary>Icon name understood by the frontend icon set. Null for group rows.</summary>
    public string Icon { get; set; }

    /// <summary>
    /// Route path. Null means the row is a grouping header only — it has no page
    /// of its own and is shown purely because a child of it is accessible.
    /// </summary>
    public string Path { get; set; }

    public int? ParentId { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Permission Parent { get; set; }
    public virtual ICollection<Permission> Children { get; set; } = new List<Permission>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
