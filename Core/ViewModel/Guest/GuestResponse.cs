using System;
using System.Collections.Generic;

namespace Core.ViewModel.Guest;

public class GuestResponse
{
    /// <summary>The EventGuest (participation) PublicId — this is what every
    /// per-event action keys off: edit, delete, travel, services, seating,
    /// sessions. NOT the person's id; see <see cref="PersonId"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>The underlying person's PublicId (Guests.PublicId). Stable across
    /// every event this human attends — use it to answer "where else is this
    /// guest?" or to add them to another event.</summary>
    public Guid PersonId { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; }
    public Guid EventId { get; set; }
    public string GuestType { get; set; }
    public string Organization { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? NationalityId { get; set; }
    public string NationalityName { get; set; }
    public string NationalityCode { get; set; }
    public string NationalityFlag { get; set; }
    public Guid? ServiceLevelId { get; set; }
    public string ServiceLevelName { get; set; }
    public string ServiceLevelNameAr { get; set; }
    public string ServiceLevelColor { get; set; }
    public bool ServiceLevelRulesOverridden { get; set; }
    public string ServiceLevelOverrideReason { get; set; }
    // ArrivalDate/DepartureDate deliberately absent: a guest's arrival and
    // departure are properties of their Flight booking, which carries them per
    // leg. Duplicating them here let the two disagree.
    public string PhotoUrl { get; set; }
    public bool AccreditationRequired { get; set; }
    // GuestServiceType values the guest may self-request from the VIP app
    // (1 = flight, 2 = accommodation, 3 = transport). See Guest.AllowedServicesJson.
    public List<int> AllowedServices { get; set; } = new();
    public List<Guid> SessionIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    // Merged in from the guest's Invitation row (if any) — not an AutoMapper
    // field, GuestService sets these after mapping.
    public string InvitationStatus { get; set; }
    public string AccreditationStatus { get; set; }
    public Guid? InvitationTemplateId { get; set; }
}
