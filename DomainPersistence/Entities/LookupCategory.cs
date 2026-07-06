using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>
/// A named group of lookup values (e.g. "AIRPORT", "HOTEL"), addressable by its
/// unique <see cref="Code"/>. Seeded as system data — the set of categories is fixed.
/// </summary>
public class LookupCategory : Entity
{
    public string Code { get; set; }        // Unique, uppercase — e.g. "AIRPORT"
    public string Name { get; set; }         // "Airports"
    public string NameAr { get; set; }       // "المطارات"
    public string Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }       // System categories cannot be deleted

    public virtual ICollection<LookupItem> Items { get; set; } = new List<LookupItem>();
}
