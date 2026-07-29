using System;
using System.Collections.Generic;

namespace Core.ViewModel.Guest;


public class CreateGuestRequest
{
    public Guid? Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public Guid EventId { get; set; }
    public string GuestType { get; set; }
    // Free-text fallback — only CSV import uses this now (no org id to resolve
    // there). The Add/Edit Guest UI sends OrganizationId instead.
    public string Organization { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? NationalityId { get; set; }
    public string Tier { get; set; }
    public List<Guid>? SessionIds { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public string PhotoUrl { get; set; }
    public bool AccreditationRequired { get; set; }
    // Selecting a template sends (or resends) the invitation email.
    public Guid? InvitationTemplateId { get; set; }
}
