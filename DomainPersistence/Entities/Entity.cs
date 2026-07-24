using System;

namespace DomainPersistence.Entities;

/// <summary>
/// Base entity: integer identity for internal keys/relations, plus a public
/// Guid used at the API boundary (routes/DTOs) so internal ids never leak.
/// </summary>
public abstract class Entity : AuditEntity
{
    // Internal identity. All FKs/joins use this.
    public int Id { get; set; }

    // API-facing identifier. Unique, DB-defaulted to newid() (see DbContext convention).
    public Guid PublicId { get; set; }
}
