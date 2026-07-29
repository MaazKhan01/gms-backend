using System;
using DomainPersistence.Enums;

namespace DomainPersistence.Entities;

/// <summary>Driver-specific details attached to a User with the driver role.</summary>
public class DriverProfile : Entity
{
    public int UserId { get; set; }
    // Nullable: drivers invited before this column existed have no value.
    public DriverType? DriverType { get; set; }
    public string LicenseNumber { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public int? NationalityId { get; set; }
    public string PhotoUrl { get; set; }

    // Self-toggled by the driver app — whether this driver currently receives
    // on-demand ride-request broadcasts. Defaults to false (opt-in).
    public bool IsAvailable { get; set; }
    public DateTime? AvailabilityChangedAt { get; set; }

    public virtual User User { get; set; }
    public virtual Nationality Nationality { get; set; }
}
