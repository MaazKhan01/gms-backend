namespace DomainPersistence.Entities;

public class Nationality
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; }      // "Saudi Arabia"
    public string NameAr { get; set; }    // "المملكة العربية السعودية"
    public string Code { get; set; }      // "SA" ISO 3166-1 alpha-2
    public string Flag { get; set; }      // "🇸🇦" emoji flag

    public virtual ICollection<Guest> Guests { get; set; } = new List<Guest>();
}
