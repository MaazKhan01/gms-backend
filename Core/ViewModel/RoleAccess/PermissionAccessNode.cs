using System;
using System.Collections.Generic;

namespace Core.ViewModel.RoleAccess;

/// <summary>
/// One menu/submenu in the access tree, with the role's two independent flags and
/// its children nested underneath. Read says nothing about Write and vice versa.
/// </summary>
public class PermissionAccessNode
{
    public Guid PermissionId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Icon { get; set; }
    /// <summary>Null for a grouping row that has no page of its own.</summary>
    public string Path { get; set; }
    public int SortOrder { get; set; }

    public bool Read { get; set; }
    public bool Write { get; set; }

    public List<PermissionAccessNode> Children { get; set; } = new();
}
