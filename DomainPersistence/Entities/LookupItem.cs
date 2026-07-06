namespace DomainPersistence.Entities;

/// <summary>
/// A single value inside a <see cref="LookupCategory"/>. Universal columns cover
/// the common case; <see cref="Metadata"/> holds a JSON blob of category-specific
/// extras (e.g. an airport's city/country, a hotel's address).
/// </summary>
public class LookupItem : Entity
{
    public Guid CategoryId { get; set; }
    public string Code { get; set; }         // e.g. "DOH", "QR" — optional per category
    public string Name { get; set; }         // e.g. "Hamad International Airport"
    public string NameAr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>JSON object of category-specific fields. Null/empty when none.</summary>
    public string Metadata { get; set; }

    public virtual LookupCategory Category { get; set; }
}
