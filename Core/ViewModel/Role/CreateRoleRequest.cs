using System;
using System.Collections.Generic;

namespace Core.ViewModel.Role;

public class CreateRoleRequest
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    // Null = leave at the default (allowed). False for driver-only roles.
    public bool? PortalAccess { get; set; }
    /// <summary>Offered as a MISSION role when nominating someone — what the
    /// person does on the mission, which is a different question from what
    /// their login can reach. Null = not a mission role.</summary>
    public bool? IsDelegateRole { get; set; }
    public List<Guid> PermissionIds { get; set; }
}
