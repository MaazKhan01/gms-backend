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
    public string Organization { get; set; }
    public Guid? NationalityId { get; set; }
    public string Tier { get; set; }
    public string InvitationStatus { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public string FlightNumber { get; set; }
    public Guid? SeatId { get; set; }
    public string Hotel { get; set; }
    public string AccreditationStatus { get; set; }
    public Guid? InvitationTemplateId { get; set; }
    public List<Guid>? SessionIds { get; set; }
}
