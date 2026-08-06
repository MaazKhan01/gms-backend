using System;

namespace Core.ViewModel.Guest;

/// <summary>
/// One row in the "Existing Guest" picker (Add Guest modal) — a guest who
/// already exists under a DIFFERENT event. Selecting one prefills a brand-new
/// Guest row for the current event (Guest is scoped to exactly one event —
/// there is no shared cross-event identity to link to); Tier and any
/// travel/services are deliberately NOT carried over here.
/// </summary>
public class OtherEventGuestRow
{
    public Guid Id { get; set; }
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
    // Legacy display string — no longer editable in the picker; superseded by
    // ServiceLevelId below. Kept for guests who predate service levels.
    public string Tier { get; set; }
    // Prefilled as the row's default in the picker table's editable Service
    // Level column — the admin can change it per-row before adding. Not
    // carried as an assignment on the new guest row; it is just the default.
    public Guid? ServiceLevelId { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelColor { get; set; }
    // Display only (their previous booking's status) — never applied to the
    // new guest, which always starts as not-sent / not-issued.
    public bool AccreditationRequired { get; set; }
    public string InvitationStatus { get; set; }
    public string AccreditationStatus { get; set; }
    public Guid EventId { get; set; }
    public string EventTitle { get; set; }
}
