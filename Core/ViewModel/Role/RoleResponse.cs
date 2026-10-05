using System;

namespace Core.ViewModel.Role;

/// <summary>
/// The role itself. What it can SEE and CHANGE is not carried here — that lives
/// in RolePermissions and is read and written through /role-access, so a role and
/// its access are never two half-synchronised copies of the same thing.
/// </summary>
public class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public bool PortalAccess { get; set; }
    /// <summary>Offered when nominating someone to a mission, so nomination lists
    /// delegate roles rather than every portal role.</summary>
    public bool IsDelegateRole { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
