namespace DomainPersistence.Enums;

/// <summary>How a vehicle is engaged for the event — the fleet-side mirror of
/// <see cref="DriverType"/>. Stored as its int value on Vehicles.UsageType, and
/// served to the client by GET /v1/lookups/enums/vehicle-usage-types.
///
/// Note the pairing is deliberately crossed: a Fixed vehicle is dedicated to one
/// Open driver (who roams and needs a car of their own), while an Open vehicle is
/// shared from the pool by Fixed drivers (who are dedicated to a guest and draw a
/// car per trip). See DriverProfile.AssignedVehicleId.
///
/// Not named VehicleType — that is already an entity, the car's category
/// (sedan/SUV/bus).</summary>
public enum VehicleUsageType
{
    Fixed = 1,
    Open = 2,
}
