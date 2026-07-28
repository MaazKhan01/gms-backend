namespace DomainPersistence.Enums;

/// <summary>How a driver is engaged for the event. Stored as its int value on
/// DriverProfiles.DriverType, and served to the client by
/// GET /v1/lookups/enums/driver-types.</summary>
public enum DriverType
{
    Fixed = 1,
    Open = 2,
}
