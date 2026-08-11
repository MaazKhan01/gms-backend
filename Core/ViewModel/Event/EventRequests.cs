using System;

namespace Core.ViewModel.Event;

public class CreateEventRequest
{
    public string Title { get; set; }
    public string Type { get; set; }
    public string Theme { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = "planning";
    /// <summary>fixed | flexible. Omit to keep the default (flexible), which is
    /// the unrestricted pre-service-level flow.</summary>
    public string GuestModel { get; set; }
    public string ImageUrl { get; set; }
    public string ThemeAccent { get; set; }
    public string ThemeSecondary { get; set; }
    public string LogoDarkUrl { get; set; }
    public string LogoLightUrl { get; set; }
}

public class UpdateEventRequest
{
    /// <summary>fixed | flexible. Omit to keep the default (flexible), which is
    /// the unrestricted pre-service-level flow.</summary>
    public string GuestModel { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string Theme { get; set; }
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; }
    public string ImageUrl { get; set; }
    public string ThemeAccent { get; set; }
    public string ThemeSecondary { get; set; }
    public string LogoDarkUrl { get; set; }
    public string LogoLightUrl { get; set; }
}

public class UpdateEventStatusRequest
{
    public string Status { get; set; }
}

public class CreateSessionRequest
{
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

public class UpdateSessionRequest : CreateSessionRequest { }
