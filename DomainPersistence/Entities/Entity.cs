using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Base entity class that provides a unique identifier for all entities
/// </summary>
public abstract class Entity : AuditEntity
{
    public Guid Id { get; set; }
}
