using System;
using DomainPersistence.Enums;

namespace Core.ViewModel.Vehicle;

public class CreateVehicleRequest
{
    // Public Guid of the VehicleType row (lookup), not its internal int id.
    public Guid VehicleTypeId { get; set; }
    // VehicleUsageType enum value: 1 = fixed, 2 = open
    // (GET /v1/lookups/enums/vehicle-usage-types). Required on new vehicles.
    public VehicleUsageType? UsageType { get; set; }
    // Public Guid of the FleetProvider row. Optional — null means in-house.
    public Guid? FleetProviderId { get; set; }
    public string VehicleModel { get; set; }
    public string VehicleNumber { get; set; }
    // Relative/absolute url produced by /v1/upload/image — the API stores the
    // string only, it never handles the file itself.
    public string VehicleImage { get; set; }
    public int? Capacity { get; set; }
}

public class UpdateVehicleRequest : CreateVehicleRequest { }

public class VehicleResponse
{
    public Guid Id { get; set; }
    public Guid VehicleTypeId { get; set; }
    public string VehicleTypeName { get; set; }
    public VehicleUsageType? UsageType { get; set; }
    // "Fixed"/"Open" — so a list can render the label without a second lookup call.
    public string UsageTypeName { get; set; }
    // True when a driver already holds this car. Only meaningful for Fixed
    // vehicles, which take one driver; Open vehicles are shared and stay false.
    public bool IsAssignedToDriver { get; set; }
    public Guid? FleetProviderId { get; set; }
    public string FleetProviderName { get; set; }
    // Event the provider is contracted for — null for an in-house vehicle, which
    // belongs to no provider and so serves every event.
    public Guid? EventId { get; set; }
    public string VehicleModel { get; set; }
    public string VehicleNumber { get; set; }
    public string VehicleImage { get; set; }
    public int? Capacity { get; set; }
}

/// <summary>One booked slot on one vehicle — the Fleet › Bookings row. Flat on
/// purpose: sorting by vehicle groups a car's slots together, sorting by driver
/// groups a driver's day, and the table needs no nesting for either.</summary>
public class VehicleBookingRow
{
    // The Transport row's public id.
    public Guid Id { get; set; }

    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; }
    public string VehicleModel { get; set; }
    public string VehicleTypeName { get; set; }
    public string VehicleImage { get; set; }
    public string FleetProviderName { get; set; }

    public Guid? DriverId { get; set; }
    public string DriverName { get; set; }
    public string DriverPhone { get; set; }

    public Guid GuestId { get; set; }
    public string GuestName { get; set; }
    public string GuestEmail { get; set; }
    public string GuestPhotoUrl { get; set; }

    public DateTime? PickupTime { get; set; }
    // Null on rows created before the drop-off became required, and on guest
    // on-demand requests. The UI shows "—" and the conflict check falls back to
    // IConflictWindowPolicy.DefaultRideDuration.
    public DateTime? DropoffTime { get; set; }
    public string Pickup { get; set; }
    public string Dropoff { get; set; }

    public string Status { get; set; }
    public string RideSource { get; set; }
}
