using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>The company that supplies vehicles to the fleet — a rental firm,
/// a ministry motor pool, a limousine contractor. Vehicles point at one.</summary>
public class FleetProvider : Entity
{
    public string Name { get; set; }

    public string ContactPerson { get; set; }

    public string Phone { get; set; }

    public string Email { get; set; }

    public string Notes { get; set; }

    public virtual ICollection<Vehicle> Vehicles { get; set; }
}
