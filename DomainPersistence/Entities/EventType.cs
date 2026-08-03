namespace DomainPersistence.Entities;

/// <summary>Category of event — Conference / Sports / Exhibition / ... — admin-managed.</summary>
public class EventType : Entity
{
    public string Name { get; set; }
}
