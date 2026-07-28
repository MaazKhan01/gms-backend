namespace DomainPersistence.Entities;

/// <summary>
/// An organisation guests / users can be affiliated with. Its physical address
/// lives in the shared Locations table as a row of type "organization", created
/// alongside the organisation itself.
/// </summary>
public class Organization : Entity
{
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Code { get; set; }
    public int? LocationId { get; set; }

    public virtual Location Location { get; set; }
}
