using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Base entity class that provides audit tracking for all entities
/// </summary>
public abstract class AuditEntity
{
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Marks the entity as deleted (soft delete)
    /// </summary>
    public void MarkAsDeleted(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets creation audit information
    /// </summary>
    public void SetCreationAudit(Guid createdBy)
    {
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets update audit information
    /// </summary>
    public void SetUpdateAudit(Guid updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
