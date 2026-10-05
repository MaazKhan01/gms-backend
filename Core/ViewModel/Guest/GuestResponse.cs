using System;
using System.Collections.Generic;

namespace Core.ViewModel.Guest;

/// <summary>
/// One guest as the admin portal sees them: a person (<c>personId</c>, name,
/// email, photo) shown through ONE event participation.
/// </summary>
/// <remarks>
/// <b>Frontend contract:</b> <see cref="Id"/> is the <c>EventGuest.PublicId</c>,
/// not the person's id — it is what every event-scoped call takes
/// (GET/PUT/DELETE /guest/{id}, seating, travel, meetings, transport).
/// <see cref="PersonId"/> is the master <c>Guest.PublicId</c>, stable across
/// every event the same human attends; use it only for person-level things
/// (notifications, support chat, "same person, other events").
/// </remarks>
public class GuestResponse
{
    /// <summary>EventGuest.PublicId — this participation. The id all guest CRUD takes.</summary>
    public Guid Id { get; set; }

    /// <summary>Guest.PublicId — the person, shared across all their events.</summary>
    public Guid PersonId { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; }
    public Guid EventId { get; set; }
    public string GuestType { get; set; }
    /// <summary>The mission role held on this participation, if any.</summary>
    public Guid? MissionRoleId { get; set; }
    public string MissionRoleName { get; set; }

    // Nomination details — see CreateGuestRequest. Returned so the edit form
    // round-trips what was entered instead of blanking it on every save.
    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public string JobTitle { get; set; }
    public string EmploymentGrade { get; set; }
    public string PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public string VisaStatus { get; set; }
    public string InsuranceStatus { get; set; }
    public string Organization { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? NationalityId { get; set; }
    public string NationalityName { get; set; }
    public string NationalityCode { get; set; }
    public string NationalityFlag { get; set; }
    /// <summary>Legacy grade string, mirrored from ServiceLevel.Code. Kept so
    /// existing consumers (chips, CSV export, VIP-app seating category) work
    /// unchanged — new code should prefer ServiceLevelName/ServiceLevelColor.</summary>
    public string Tier { get; set; }
    public Guid? ServiceLevelId { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelNameAr { get; set; }
    public string ServiceLevelColor { get; set; }
    public bool ServiceLevelRulesOverridden { get; set; }
    public string ServiceLevelOverrideReason { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public string PhotoUrl { get; set; }
    public bool AccreditationRequired { get; set; }
    // GuestServiceType values the guest may self-request from the VIP app
    // (1 = flight, 2 = accommodation, 3 = transport). See EventGuest.AllowedServicesJson.
    public List<int> AllowedServices { get; set; } = new();
    public List<Guid> SessionIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    // Merged in from this participation's Invitation row (if any) — not an
    // AutoMapper field, GuestService sets these after mapping.
    public string InvitationStatus { get; set; }
    public string AccreditationStatus { get; set; }
    public Guid? InvitationTemplateId { get; set; }
}
