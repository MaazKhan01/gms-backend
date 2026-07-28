using System;

namespace Core.ViewModel.Vehicle;

public class CreateVehicleRequest
{
    // Public Guid of the VehicleType row (lookup), not its internal int id.
    public Guid VehicleTypeId { get; set; }
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
    public string VehicleModel { get; set; }
    public string VehicleNumber { get; set; }
    public string VehicleImage { get; set; }
    public int? Capacity { get; set; }
}
