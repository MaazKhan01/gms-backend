namespace DomainPersistence.Entities;

/// <summary>The eligible-driver pool for a guest's stay — admin assigns one or
/// more drivers here; the specific driver on each Transport row is picked from
/// (but not restricted to) this pool.</summary>
public class GuestDriverAssignment : Entity
{
    public int EventGuestId { get; set; }
    public int DriverId { get; set; }

    public virtual EventGuest EventGuest { get; set; }
    public virtual DriverProfile Driver { get; set; }
}
