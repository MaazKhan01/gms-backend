using System;
using System.Collections.Generic;

namespace Core.ViewModel.Guest;


public class CreateGuestRequest
{
    public Guid? Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }

    /// <summary>Required — this is the person's identity. If a Guest already exists
    /// with this email, no new person is created: see <see cref="LinkExistingPerson"/>.</summary>
    public string Email { get; set; }

    public Guid EventId { get; set; }

    /// <summary>
    /// "I know this person already exists, add them to this event too."
    /// <para>
    /// false (the New Guest wizard): a matching email is rejected, pointing the user
    /// at Add Existing Guest — creating a second person record for the same human is
    /// exactly what this schema exists to prevent.
    /// </para>
    /// <para>
    /// true (Add Existing Guest): the existing person is reused and only a new
    /// EventGuest row is written. Still rejected if they're already in this event.
    /// </para>
    /// </summary>
    public bool LinkExistingPerson { get; set; }
    public string GuestType { get; set; }
    // Free-text fallback — only CSV import uses this now (no org id to resolve
    // there). The Add/Edit Guest UI sends OrganizationId instead.
    public string Organization { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? NationalityId { get; set; }
    public Guid? ServiceLevelId { get; set; }
    // Set by the UI when the user chose to push through a failing Service Level
    // rule. Requires PermissionCodes.ServiceLevelsOverrideRules — the service
    // re-checks, so a client can't grant itself the bypass.
    public bool OverrideServiceLevelRules { get; set; }
    public string ServiceLevelOverrideReason { get; set; }
    public List<Guid>? SessionIds { get; set; }
    // ArrivalDate/DepartureDate removed: a guest's arrival and departure are the
    // Flight booking's, saved through the travel endpoints.
    public string PhotoUrl { get; set; }
    public bool AccreditationRequired { get; set; }
    // Which services this guest may request from the VIP app on their own —
    // GuestServiceType values (1 = flight, 2 = accommodation, 3 = transport).
    // Unrecognised values are dropped; null/empty means none.
    public List<int> AllowedServices { get; set; }
    // Selecting a template sends (or resends) the invitation email.
    public Guid? InvitationTemplateId { get; set; }
}
