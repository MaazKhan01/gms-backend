using System;
using System.Collections.Generic;

namespace Core.ViewModel.Guest;


/// <summary>
/// Create or update one guest participation.
/// </summary>
/// <remarks>
/// <b>Frontend contract:</b> on update, <see cref="Id"/> is the
/// <c>EventGuest.PublicId</c> (what <c>GuestResponse.Id</c> returned), not the
/// person's id. On create, an <see cref="Email"/> that already belongs to a
/// guest reuses that master Guest and their login, adding a second
/// participation — that is how "add an existing guest to this event" works.
/// <see cref="Email"/> is required and cannot be changed once created.
/// </remarks>
public class CreateGuestRequest
{
    /// <summary>Update only: the EventGuest.PublicId being edited.</summary>
    public Guid? Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    /// <summary>Required. Trimmed/lowercased server-side, unique per person.
    /// Immutable after creation — sending a different one on update is rejected.</summary>
    public string Email { get; set; }
    public Guid EventId { get; set; }
    public string GuestType { get; set; }
    /// <summary>
    /// What this person does ON the mission — a Roles row flagged
    /// IsDelegateRole (Head of Delegation, Member, Support Staff). Distinct
    /// from GuestType, which is the GMS category of guest; in DMS the mission
    /// role is the one that carries meaning, and the readiness, protocol-order
    /// and nomination-letter screens all read it.
    /// </summary>
    public Guid? MissionRoleId { get; set; }

    // ── Nomination details ───────────────────────────────────────────────
    // What HR later verifies, captured while the delegate is being entered
    // rather than left for someone to chase afterwards. The first four belong
    // to the PERSON and follow them between missions; visa and insurance
    // belong to this participation, because a visa is for one trip.
    //
    // Department is what makes someone staff: without it they can be a delegate
    // on this mission but never appear in the pool for the next one.
    public Guid? DepartmentId { get; set; }
    public string JobTitle { get; set; }
    public string EmploymentGrade { get; set; }
    public string PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }

    /// <summary>Core.Constants.VisaStatuses.</summary>
    public string VisaStatus { get; set; }
    /// <summary>Core.Constants.InsuranceStatuses.</summary>
    public string InsuranceStatus { get; set; }
    // Free-text fallback — only CSV import uses this now (no org id to resolve
    // there). The Add/Edit Guest UI sends OrganizationId instead.
    public string Organization { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? NationalityId { get; set; }
    // Legacy free-text grade — only CSV import still sends this. The Add/Edit
    // Guest UI sends ServiceLevelId, and the backend mirrors the level's Code
    // back onto Tier so string-based consumers keep working.
    public string Tier { get; set; }
    public Guid? ServiceLevelId { get; set; }
    // Set by the UI when the user chose to push through a failing Service Level
    // rule. Requires PermissionCodes.ServiceLevelsOverrideRules — the service
    // re-checks, so a client can't grant itself the bypass.
    public bool OverrideServiceLevelRules { get; set; }
    public string ServiceLevelOverrideReason { get; set; }
    public List<Guid>? SessionIds { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public string PhotoUrl { get; set; }
    public bool AccreditationRequired { get; set; }
    // Which services this guest may request from the VIP app on their own —
    // GuestServiceType values (1 = flight, 2 = accommodation, 3 = transport).
    // Unrecognised values are dropped; null/empty means none.
    public List<int> AllowedServices { get; set; }
    // Selecting a template sends (or resends) the invitation email.
    public Guid? InvitationTemplateId { get; set; }
}
