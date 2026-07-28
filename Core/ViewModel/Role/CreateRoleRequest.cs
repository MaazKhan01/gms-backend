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
    public List<Guid> PermissionIds { get; set; }
}
