using System;
using System.Collections.Generic;

namespace Core.ViewModel.RoleAccess;

/// <summary>
/// What the signed-in user's own role may reach — the navigation tree plus the
/// read/write flags the UI enforces with. Pruned: a row only appears if the role
/// can read it or one of its descendants is readable (so a parent shows up for the
/// sake of its accessible submenu, without needing a record of its own).
/// </summary>
public class MyAccessResponse
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; }
    public string RoleCode { get; set; }
    public List<PermissionAccessNode> Permissions { get; set; } = new();
}
