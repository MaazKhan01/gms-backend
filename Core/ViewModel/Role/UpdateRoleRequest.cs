using System;
using System.Collections.Generic;

namespace Core.ViewModel.Role;

public class UpdateRoleRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<Guid> PermissionIds { get; set; }
}
