using System;
using System.Collections.Generic;

namespace Core.ViewModel.RoleAccess;

/// <summary>The menu tree on its own, with no role attached (GET /role-access/permissions).</summary>
public class PermissionNode
{
    public Guid PermissionId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Icon { get; set; }
    public string Path { get; set; }
    public int SortOrder { get; set; }
    public List<PermissionNode> Children { get; set; } = new();
}
