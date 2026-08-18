namespace DomainPersistence.Entities;

/// <summary>A hotel available for guest accommodation.</summary>
public class AccommodationHotel : Entity
{
    public string Name { get; set; }
    public string Address { get; set; }
    public string ImageUrl { get; set; }
    public int? LocationId { get; set; }
    public virtual Location Location { get; set; }
}
