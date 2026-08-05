using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>The company that supplies vehicles to the fleet — a rental firm,
/// a ministry motor pool, a limousine contractor. Vehicles point at one.</summary>
public class FleetProvider : Entity
{
    // Providers are contracted per event: the same rental firm working two
    // events is two rows, so its contact/notes can differ per contract and one
    // event's fleet never shows up in another's dropdowns.
    public int EventId { get; set; }

    public string Name { get; set; }

    public string ContactPerson { get; set; }

    public string Phone { get; set; }

    public string Email { get; set; }

    public string Notes { get; set; }

    public virtual Event Event { get; set; }

    public virtual ICollection<Vehicle> Vehicles { get; set; }
}
