using System;
using DomainPersistence.Enums;

namespace DomainPersistence.Entities;

/// <summary>Driver-specific details attached to a User with the driver role.</summary>
public class DriverProfile : Entity
{
    public int UserId { get; set; }
    // Nullable: drivers invited before this column existed have no value.
    public DriverType? DriverType { get; set; }
    // Open drivers toggle this from the driver app to say they're taking work.
    // Meaningless for Fixed drivers — they're assigned regardless.
    public bool IsOnline { get; set; }
    public string LicenseNumber { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public int? NationalityId { get; set; }
    public string PhotoUrl { get; set; }
    public virtual User User { get; set; }
    public virtual Nationality Nationality { get; set; }
}
