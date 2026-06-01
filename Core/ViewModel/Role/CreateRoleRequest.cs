using System;
using System.Collections.Generic;

namespace Core.ViewModel.Role;

public class CreateRoleRequest
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public List<Guid> PermissionIds { get; set; }
}
