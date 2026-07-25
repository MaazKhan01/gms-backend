namespace DomainPersistence.Entities;

/// <summary>Venue layout element/shape type — round / rect / stadium / stage / ...
/// Code drives the editor's shape behaviour; Name is the palette label.</summary>
public class ElementType : Entity
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
}
