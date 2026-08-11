using System;
using System.Collections.Generic;

namespace Core.ViewModel.Event;

public class EventResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string Theme { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; }
    public string AppKey { get; set; }
    /// <summary>fixed | flexible — whether this event runs the Service Level flow.</summary>
    public string GuestModel { get; set; }
    public string ImageUrl { get; set; }
    public string ThemeAccent { get; set; }
    public string ThemeSecondary { get; set; }
    public string LogoDarkUrl { get; set; }
    public string LogoLightUrl { get; set; }
    public List<SessionResponse> Sessions { get; set; } = new();
}

public class SessionResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Title { get; set; }
    public DateOnly? Date { get; set; }
    public string Time { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public string Room { get; set; }
    public string Speaker { get; set; }
    public int Capacity { get; set; }
    public string ImageUrl { get; set; }
}
