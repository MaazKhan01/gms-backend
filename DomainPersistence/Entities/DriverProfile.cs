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

    // The car this driver keeps permanently, set when the driver is invited.
    // Only Open drivers get one, and it must be a Fixed vehicle — an Open driver
    // roams and needs a dedicated car, whereas a Fixed driver is tied to a guest
    // and draws an Open pool car per trip through Transport.VehicleId instead.
    // Null for every Fixed driver, and for Open drivers invited before this existed.
    public int? AssignedVehicleId { get; set; }

    public virtual User User { get; set; }
    public virtual Nationality Nationality { get; set; }
    public virtual Vehicle AssignedVehicle { get; set; }
}
