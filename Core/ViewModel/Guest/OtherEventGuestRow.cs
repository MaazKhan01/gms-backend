using System;

namespace Core.ViewModel.Guest;

/// <summary>
/// One row in the "Existing Guest" picker (Add Guest modal) — a person who
/// already participates in a DIFFERENT event.
/// </summary>
/// <remarks>
/// Picking one and POSTing /guest with the same email adds a SECOND
/// <c>EventGuest</c> against the same master <c>Guest</c> — the person, their
/// login and their profile are reused, only the participation is new. Service
/// level, invitation and accreditation shown here are that other event's and
/// are never carried over.
/// </remarks>
public class OtherEventGuestRow
{
    /// <summary>EventGuest.PublicId of their participation in the other event.</summary>
    public Guid Id { get; set; }
    /// <summary>Guest.PublicId — the person that would be reused.</summary>
    public Guid PersonId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string GuestType { get; set; }
    public Guid? OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public Guid? NationalityId { get; set; }
    public string NationalityName { get; set; }
    public string NationalityFlag { get; set; }
    public string PhotoUrl { get; set; }
    // Legacy display string — superseded by ServiceLevelId below.
    public string Tier { get; set; }
    // Prefilled as the row's default in the picker table's editable Service
    // Level column — the admin can change it per-row before adding. Not
    // carried as an assignment on the new participation; it is just the default.
    public Guid? ServiceLevelId { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelColor { get; set; }
    // Display only (their other event's state) — never applied to the new
    // participation, which always starts as not-sent / not-issued.
    public bool AccreditationRequired { get; set; }
    public string InvitationStatus { get; set; }
    public string AccreditationStatus { get; set; }
    public Guid EventId { get; set; }
    public string EventTitle { get; set; }
}
