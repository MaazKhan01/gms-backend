using System;
using System.Collections.Generic;

namespace Core.ViewModel.Guest;

public class GuestResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; }
    public Guid EventId { get; set; }
    public string GuestType { get; set; }
    public string Organization { get; set; }
    public Guid? NationalityId { get; set; }
    public string NationalityName { get; set; }
    public string NationalityCode { get; set; }
    public string NationalityFlag { get; set; }
    public string Tier { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public List<Guid> SessionIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    // Merged in from the guest's Invitation row (if any) — not an AutoMapper
    // field, GuestService sets these after mapping.
    public string InvitationStatus { get; set; }
    public string AccreditationStatus { get; set; }
    public Guid? InvitationTemplateId { get; set; }
}
