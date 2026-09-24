using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>Organisational department a person belongs to — Protocol Office /
/// Logistics / Media &amp; Press / ... Admin-managed reference data; a Department
/// Head nominates only from their own department, so this is what that scoping
/// rule will read.</summary>
public class Department : Entity
{
    public string Name { get; set; }
    public string NameAr { get; set; }

    public virtual ICollection<Guest> Guests { get; set; } = new List<Guest>();
}
