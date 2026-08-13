namespace DomainPersistence.Entities;

/// <summary>
/// Per-guest invitation + accreditation state. Split out of Guest so the two
/// workflows (invite / accredit) have their own lifecycle and audit trail.
/// </summary>
public class Invitation : Entity
{
    public int EventGuestId { get; set; }

    public string InvitationStatus { get; set; }     // Pending | Accepted | Rejected
    public string AccreditationStatus { get; set; }   // Pending | Approved | Rejected

    public int? InvitationTemplateId { get; set; }

    // Unguessable token embedded in the invitation email's "View invitation"
    // link so the guest can open a public (no-login) page to accept/reject.
    public Guid? InvitationToken { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public virtual EventGuest EventGuest { get; set; }
    public virtual InvitationTemplate InvitationTemplate { get; set; }
}
