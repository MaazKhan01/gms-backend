using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Admin-granted cross-module read access for a specific user.
/// A row here means that user can see the named module even though their role
/// doesn't natively include its View permission.
/// </summary>
public class UserModuleGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Module slug — matches ModuleDefinitions.All keys (e.g. "guests", "seating").</summary>
    public string Module { get; set; }

    public bool IsGranted { get; set; } = true;
    public Guid GrantedBy { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; }
}
