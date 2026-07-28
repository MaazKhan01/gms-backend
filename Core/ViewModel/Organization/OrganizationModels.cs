using System;

namespace Core.ViewModel.Organization;

/// <summary>
/// The map-picked point for an organisation. Persisted as a Location row of
/// type "organization" as part of creating/updating the organisation, so the
/// client never has to create the location separately.
/// </summary>
public class OrganizationLocationInput
{
    public string Latitude { get; set; }
    public string Longitude { get; set; }
    public string Address { get; set; }
}

public class CreateOrganizationRequest
{
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Code { get; set; }
    public OrganizationLocationInput Location { get; set; }
}

public class UpdateOrganizationRequest : CreateOrganizationRequest { }

public class OrganizationResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Code { get; set; }
    public Guid? LocationId { get; set; }
    public string Address { get; set; }
    public string Latitude { get; set; }
    public string Longitude { get; set; }
}
