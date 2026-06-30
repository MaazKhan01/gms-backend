using System;

namespace DomainPersistence.Entities;

/// <summary>
/// A self-service sign-up awaiting admin approval. On approval a real
/// <see cref="User"/> is created from these details (with the chosen password
/// hash) and assigned a role; until then the person cannot log in.
/// </summary>
public partial class AccountRequest : Entity
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string PasswordHash { get; set; }    // chosen at request time (BCrypt)
    public string Note { get; set; }            // optional message from the requester

    public Guid? RequestedRoleId { get; set; }   // role the requester selected
    public string RequestedRoleName { get; set; } // denormalized for display

    public string Status { get; set; } = "pending";   // pending | approved | rejected

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string ReviewNote { get; set; }
    public Guid? CreatedUserId { get; set; }     // set when approved
}
