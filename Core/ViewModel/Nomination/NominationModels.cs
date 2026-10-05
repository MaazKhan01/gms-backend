using System;
using System.Collections.Generic;

namespace Core.ViewModel.Nomination;

/// <summary>One person on a mission's roster.</summary>
public class NominationResponse
{
    /// <summary>The participation id — what every roster-scoped route takes.</summary>
    public Guid Id { get; set; }
    /// <summary>The person behind it, stable across missions.</summary>
    public Guid PersonId { get; set; }

    public string FullName { get; set; }
    public string Email { get; set; }
    public string JobTitle { get; set; }
    public string EmploymentGrade { get; set; }
    public string PhotoUrl { get; set; }

    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; }

    public Guid? MissionRoleId { get; set; }
    public string MissionRoleName { get; set; }
    public string Subgroup { get; set; }

    /// <summary>The Groups lookup row this delegate belongs to on this mission.
    /// Null clears it. <c>Subgroup</c> is derived from it — the name is mirrored
    /// on save so the roster screens that still group by the string follow.</summary>
    public Guid? GroupId { get; set; }

    public DateTime? NominatedOn { get; set; }
    public string NominatedByName { get; set; }

    public string HrVerificationStatus { get; set; }
    public DateTime? HrVerifiedOn { get; set; }
    public string HrVerificationNote { get; set; }

    public string PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }

    /// <summary>Core.Constants.VisaStatuses. HR checks these alongside the
    /// passport, so the roster carries them rather than making the screen
    /// fetch the participation a second time.</summary>
    public string VisaStatus { get; set; }
    /// <summary>False when this delegate needs no visa for the destination —
    /// which is a different thing from having one.</summary>
    public bool VisaRequired { get; set; }
    /// <summary>Core.Constants.InsuranceStatuses.</summary>
    public string InsuranceStatus { get; set; }

    /// <summary>Warnings computed at read time — never stored, so they cannot go stale.</summary>
    public NominationFlags Flags { get; set; } = new();
}

/// <summary>Automated checks surfaced next to a nomination. All derived.</summary>
public class NominationFlags
{
    /// <summary>No passport number or no expiry recorded.</summary>
    public bool PassportMissing { get; set; }

    /// <summary>Passport expires within six months of the mission's start
    /// (or of today when the mission has no start date).</summary>
    public bool PassportExpiringSoon { get; set; }

    /// <summary>Already expired by the time the mission starts.</summary>
    public bool PassportExpired { get; set; }

    /// <summary>This person is on another mission whose dates overlap this one.</summary>
    public bool OverlappingMission { get; set; }

    /// <summary>Titles of the overlapping missions, for the tooltip.</summary>
    public List<string> OverlappingMissions { get; set; } = new();
}

/// <summary>A person who could be nominated but is not on this mission yet.</summary>
public class NominationCandidateResponse
{
    public Guid PersonId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string JobTitle { get; set; }
    public string EmploymentGrade { get; set; }
    public string PhotoUrl { get; set; }
    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; }

    /// <summary>
    /// Missions this person is already on that have not finished. Staff may
    /// serve on several — the workflow warns about overlaps rather than
    /// forbidding them — so the picker states the commitment instead of
    /// leaving it to look like the filter is broken.
    /// </summary>
    public List<string> CurrentMissions { get; set; } = new();

    /// <summary>True when one of those overlaps the mission being staffed.</summary>
    public bool OverlapsThisMission { get; set; }

    /// <summary>
    /// The overlapping missions by name. The warning has to say WHICH mission
    /// the person is double-booked on — "already on an overlapping mission" is
    /// not something a coordinator can act on without then going to look it up.
    /// </summary>
    public List<string> OverlappingMissions { get; set; } = new();

    // ── This mission ─────────────────────────────────────────────────────────
    // The screen lists the whole staff directory, so every row has to say where
    // it stands against the mission being staffed — a directory that did not
    // would make you cross-reference the roster to know who is already on it.

    /// <summary>Already nominated to this mission.</summary>
    /// <summary>Nominated to this mission. NOT the same as merely holding a
    /// participation — the Delegates screen creates those directly.</summary>
    public bool OnRoster { get; set; }
    /// <summary>Holds a participation on this mission but has not been
    /// nominated. They are listed, and the action offered is Nominate.</summary>
    public bool OnMissionNotNominated { get; set; }

    /// <summary>The participation, when they are on it. What edit and remove take.</summary>
    public Guid? ParticipationId { get; set; }

    /// <summary>The role's id, not just its label — the nominate form preselects
    /// it, and a name cannot be fed back to a dropdown keyed by id.</summary>
    public Guid? MissionRoleId { get; set; }
    public string MissionRoleName { get; set; }
    public string Subgroup { get; set; }

    /// <summary>The Groups lookup row this delegate belongs to on this mission.
    /// Null clears it. <c>Subgroup</c> is derived from it — the name is mirrored
    /// on save so the roster screens that still group by the string follow.</summary>
    public Guid? GroupId { get; set; }

    // Visa and insurance belong to the PARTICIPATION, not the person, so they
    // are null until someone is nominated. That is the honest answer, not a gap.
    public string VisaStatus { get; set; }
    public bool VisaRequired { get; set; }
    public string InsuranceStatus { get; set; }

    /// <summary>HR's decision on this participation. Null until nominated. The
    /// nominate screen does not show it as a column — the HR phase comes after
    /// this one — but it needs to know about a REJECTION, because a rejected
    /// delegate is the one case where the coordinator has to act from here.</summary>
    public string HrVerificationStatus { get; set; }
    public string HrVerificationNote { get; set; }

    public string PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    /// <summary>Expires within six months of the mission starting, or already.</summary>
    public bool PassportExpiringSoon { get; set; }
}

/// <summary>A role a delegate may hold on a mission — Roles flagged IsDelegateRole.</summary>
public class MissionRoleResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    /// <summary>True if holders of this role can also sign in to the portal.</summary>
    public bool PortalAccess { get; set; }
}

public class CreateNominationRequest
{
    public Guid EventId { get; set; }
    /// <summary>The person to nominate (Guest.PublicId).</summary>
    public Guid PersonId { get; set; }
    public Guid? MissionRoleId { get; set; }
    public string Subgroup { get; set; }

    /// <summary>The Groups lookup row this delegate belongs to on this mission.
    /// Null clears it. <c>Subgroup</c> is derived from it — the name is mirrored
    /// on save so the roster screens that still group by the string follow.</summary>
    public Guid? GroupId { get; set; }
}

public class UpdateNominationRequest
{
    public Guid? MissionRoleId { get; set; }
    public string Subgroup { get; set; }

    /// <summary>The Groups lookup row this delegate belongs to on this mission.
    /// Null clears it. <c>Subgroup</c> is derived from it — the name is mirrored
    /// on save so the roster screens that still group by the string follow.</summary>
    public Guid? GroupId { get; set; }
}

/// <summary>
/// Adds a person to the STAFF DIRECTORY — the pool nominations are drawn from.
///
/// Separate from CreateGuestRequest on purpose: that one always attaches the
/// person to an event, so it cannot express "this person works here" without
/// also putting them on a mission. A department is what makes someone staff,
/// which is why it is the one required field beyond a name and an address.
/// </summary>
public class CreateStaffRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }

    /// <summary>Required — a person with no department is not staff.</summary>
    public Guid? DepartmentId { get; set; }

    public string JobTitle { get; set; }
    public string EmploymentGrade { get; set; }
    public Guid? NationalityId { get; set; }
    public string PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }

    /// <summary>When set, the new person is nominated to this mission straight
    /// away — which is what the picker does, since that is why you are adding
    /// them.</summary>
    public Guid? EventId { get; set; }
    public Guid? MissionRoleId { get; set; }
    public string Subgroup { get; set; }

    /// <summary>The Groups lookup row this delegate belongs to on this mission.
    /// Null clears it. <c>Subgroup</c> is derived from it — the name is mirrored
    /// on save so the roster screens that still group by the string follow.</summary>
    public Guid? GroupId { get; set; }
}
