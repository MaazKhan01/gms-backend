using System;

namespace DomainPersistence.Entities;

public partial class Session : Entity
{
    public Guid EventId { get; set; }
    public string Title { get; set; }
    public DateOnly? Date { get; set; }
    public string Time { get; set; }            // "HH:mm" — matches the frontend
    public string VenueName { get; set; }
    public Guid? VenueId { get; set; }
    public string Room { get; set; }
    public string Speaker { get; set; }
    public int Capacity { get; set; }

    public virtual Event Event { get; set; }
    public virtual Venue Venue { get; set; }
}
