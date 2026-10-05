using System;
using System.Collections.Generic;

namespace Core.ViewModel.RoleAccess;

/// <summary>
/// The full permission tree for one role. Used by the Role Access admin screen,
/// so every row is present — inaccessible ones simply carry read=false/write=false.
/// </summary>
public class RoleAccessResponse
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; }
    public string RoleCode { get; set; }
    public List<PermissionAccessNode> Permissions { get; set; } = new();
}
