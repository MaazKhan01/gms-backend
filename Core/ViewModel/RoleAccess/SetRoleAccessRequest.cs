using System;
using System.Collections.Generic;

namespace Core.ViewModel.RoleAccess;

/// <summary>
/// Replaces a role's access in full — anything absent from <see cref="Items"/>,
/// or present with both flags false, ends up with no access.
/// </summary>
public class SetRoleAccessRequest
{
    public List<RoleAccessItem> Items { get; set; } = new();
}

public class RoleAccessItem
{
    public Guid PermissionId { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
}
