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
    // Display only — the picker shows what tier they held before, but it's
    // never copied onto the new event's guest.
    public string Tier { get; set; }
    public Guid EventId { get; set; }
    public string EventTitle { get; set; }
}
