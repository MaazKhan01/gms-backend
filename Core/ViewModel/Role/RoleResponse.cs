using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.ViewModel.Role;

public class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public bool PortalAccess { get; set; }
    /// <summary>Only populated by GET /roles/{id}. The list endpoint leaves it
    /// null (and therefore omits it) — callers there just need the role itself.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PermissionDto> Permissions { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string Module { get; set; }
}
