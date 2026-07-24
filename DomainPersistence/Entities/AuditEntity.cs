using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Base entity class that provides audit tracking for all entities
/// </summary>
public abstract class AuditEntity
{
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }
    public int? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Marks the entity as deleted (soft delete)
    /// </summary>
    public void MarkAsDeleted(int deletedBy)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets creation audit information
    /// </summary>
    public void SetCreationAudit(int createdBy)
    {
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets update audit information
    /// </summary>
    public void SetUpdateAudit(int updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
