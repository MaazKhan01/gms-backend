using System;

namespace Core.ViewModel.FleetProvider;

public class CreateFleetProviderRequest
{
    public string Name { get; set; }
    public string ContactPerson { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Notes { get; set; }
}

public class UpdateFleetProviderRequest : CreateFleetProviderRequest { }

public class FleetProviderResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string ContactPerson { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Notes { get; set; }
    public int VehicleCount { get; set; }
}
